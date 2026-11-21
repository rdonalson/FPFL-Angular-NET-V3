#!/usr/bin/env bash
# One-time cluster setup for UI-NG (Angular) and API-NET (.NET), next to the React/Java apps:
#   1. a dedicated 2-node pool (netpool) that only these two apps run on
#   2. the api-net-secret (database connection string + JWT signing key)
#   3. DNS records for ui-ng / api-net (Azure DNS zone, or printed for your registrar)
# Building images and deploying is done by the GitHub Actions workflows in .github/workflows.
#
# Run from Azure Cloud Shell (Bash):
#   git clone https://github.com/rdonalson/FPFL-Angular-NET-V3.git
#   cd FPFL-Angular-NET-V3
#   bash k8s/setup-fpfl-net.sh
#
# Safe to re-run: anything that already exists is left as is.
set -euo pipefail

# ----------------------------------------------------------------------------- names
RESOURCE_GROUP="FPFL-React-Java"
CLUSTER="fpfl-cluster"
NODEPOOL="netpool"
NODE_COUNT=2
NODE_SIZE="Standard_B2s"
NAMESPACE="fpfl"
JAVA_DB_SECRET="api-db-secret"      # existing Java secret: DB_URL, DB_USERNAME, DB_PASSWORD
NET_SECRET="api-net-secret"         # new: ConnectionStrings__Fpfl, Auth__SigningKey
DOMAIN="ledger-finance.com"
UI_HOST="ui-ng"
API_HOST="api-net"


step() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }

# ----------------------------------------------------------------------------- 1. cluster
step "Connecting kubectl to ${CLUSTER}"
az aks get-credentials -g "$RESOURCE_GROUP" -n "$CLUSTER" --overwrite-existing >/dev/null

# ----------------------------------------------------------------------------- 2. node pool
step "Node pool ${NODEPOOL} (${NODE_COUNT} x ${NODE_SIZE})"
if az aks nodepool show -g "$RESOURCE_GROUP" --cluster-name "$CLUSTER" -n "$NODEPOOL" >/dev/null 2>&1; then
  echo "Already exists; leaving it as is."
else
  # The taint keeps everything else off these nodes; the UI-NG/API-NET pods tolerate it.
  az aks nodepool add \
    -g "$RESOURCE_GROUP" --cluster-name "$CLUSTER" -n "$NODEPOOL" \
    --mode User --os-type Linux \
    --node-count "$NODE_COUNT" --node-vm-size "$NODE_SIZE" \
    --labels workload=fpfl-net \
    --node-taints workload=fpfl-net:NoSchedule \
    -o table
fi

# ----------------------------------------------------------------------------- 3. secret
step "Secret ${NET_SECRET}"
if kubectl get secret "$NET_SECRET" -n "$NAMESPACE" >/dev/null 2>&1; then
  echo "Already exists; keeping it (and the signing key, so nobody gets signed out)."
else
  secret_value() { kubectl get secret "$JAVA_DB_SECRET" -n "$NAMESPACE" -o "jsonpath={.data.$1}" | base64 -d; }
  DB_URL="$(secret_value DB_URL)"            # jdbc:postgresql://host:5432/FPFL-V2
  DB_USER="$(secret_value DB_USERNAME)"
  DB_PASS="$(secret_value DB_PASSWORD)"

  hostport="${DB_URL#jdbc:postgresql://}"; hostport="${hostport%%/*}"
  DB_HOST="${hostport%%:*}"
  DB_PORT="${hostport##*:}"; [[ "$DB_PORT" == "$DB_HOST" ]] && DB_PORT=5432
  DB_NAME="${DB_URL##*/}"; DB_NAME="${DB_NAME%%\?*}"

  # Quote the password so characters like ; don't break the connection string.
  CONN="Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=\"${DB_PASS//\"/\"\"}\";SSL Mode=Require"

  kubectl create secret generic "$NET_SECRET" -n "$NAMESPACE" \
    --from-literal="ConnectionStrings__Fpfl=${CONN}" \
    --from-literal="Auth__SigningKey=$(openssl rand -base64 48)"
  echo "Created from ${JAVA_DB_SECRET} (database ${DB_NAME} on ${DB_HOST}) with a new signing key."
fi

# ----------------------------------------------------------------------------- 4. DNS
step "DNS for ${UI_HOST}.${DOMAIN} and ${API_HOST}.${DOMAIN}"
INGRESS_IP="$(kubectl get ingress fpfl-api-ingress -n "$NAMESPACE" -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || true)"
if [[ -z "$INGRESS_IP" ]]; then
  INGRESS_IP="$(kubectl get svc -A -l app.kubernetes.io/component=controller \
    -o jsonpath='{.items[0].status.loadBalancer.ingress[0].ip}' 2>/dev/null || true)"
fi
echo "Ingress IP: ${INGRESS_IP:-unknown}"

ZONE_RG="$(az network dns zone list --query "[?name=='${DOMAIN}'].resourceGroup | [0]" -o tsv 2>/dev/null || true)"
if [[ -n "$ZONE_RG" && -n "$INGRESS_IP" ]]; then
  for host in "$UI_HOST" "$API_HOST"; do
    if az network dns record-set a show -g "$ZONE_RG" -z "$DOMAIN" -n "$host" >/dev/null 2>&1; then
      echo "A record ${host}.${DOMAIN} already exists; leaving it."
    else
      az network dns record-set a add-record -g "$ZONE_RG" -z "$DOMAIN" -n "$host" -a "$INGRESS_IP" -o none
      echo "Added A record ${host}.${DOMAIN} -> ${INGRESS_IP}"
    fi
  done
else
  echo "No Azure DNS zone for ${DOMAIN} found here. At your DNS provider, add:"
  echo "  A  ${UI_HOST}   -> ${INGRESS_IP:-<ingress IP>}"
  echo "  A  ${API_HOST}  -> ${INGRESS_IP:-<ingress IP>}"
fi

# ----------------------------------------------------------------------------- 5. status
step "Status"
kubectl get nodes -l workload=fpfl-net -o wide
cat <<DONE

Setup done. Next: run the two GitHub Actions workflows (Actions tab -> "Build and Push FPFL
API-NET to ACR" / "... UI-NG ..." -> Run workflow), or push a change under API-NET/ or UI-NG/.
Certificates are issued once DNS resolves. Then try:
  https://${UI_HOST}.${DOMAIN}
  https://${API_HOST}.${DOMAIN}/swagger-ui
DONE
