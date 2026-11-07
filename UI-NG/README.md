# UI-NG — Angular Frontend for FPFL-Angular-NET

UI-NG is the Angular 22 + TypeScript frontend for the FPFL platform.
It is built entirely from standalone components (no NgModules), runs zoneless with signals, and integrates cleanly with the .NET 10 API.

It replaces the original version's NgModule/NgRx front end, keeping the same product while adopting the architecture used in the sister [UI-TSX React app](https://github.com/rdonalson/FPFL-React-Java/tree/main/UI-TSX).

Live at <https://ui-ng.ledger-finance.com> · API at <https://api-net.ledger-finance.com>

> 🚧 **Nearly at parity, and past it.** Everything in UI-TSX is here — credits and debits, the initial amount, and the forecast with its chart and ledger — plus three things UI-TSX doesn't have: **Export to Excel**, **Cards & Loans** (V3) and **Debt Paydown**. The **Docs** page and the admin **System Status** page now match UI-TSX's too. See [Project Status](#-project-status).

---

## 🚀 Tech Stack

| Area | Choice |
| ---- | ------ |
| **Framework** | Angular 22 + TypeScript 6 — standalone components, **zoneless** change detection, `OnPush` by default |
| **Build tool** | Angular CLI with the esbuild‑based `@angular/build:application` builder |
| **UI components** | PrimeNG 22 · PrimeIcons 8 |
| **Charts** | Chart.js 4 through PrimeNG's `p-chart` |
| **Excel export** | ExcelJS 4, lazy‑loaded on first export |
| **Theming** | `@primeuix/themes` **Aura** preset (dark mode ready via an `.app-dark` class) · SCSS with CSS custom properties |
| **State** | Angular **signals** (`signal`, `computed`, `effect`) — no NgRx store |
| **Server state** | **`httpResource`** for reads (loading / value / error as signals), `HttpClient` + RxJS for writes |
| **Routing** | Angular Router with lazy `loadComponent` routes and functional guards |
| **Forms** | Reactive Forms (`NonNullableFormBuilder`) with built‑in and custom validators |
| **HTTP** | `provideHttpClient(withFetch())` with functional interceptors |
| **Auth** | JWT access tokens + single‑use refresh tokens, silent refresh, expiry warning |
| **Testing** | Vitest + jsdom via `@angular/build:unit-test`, `HttpTestingController` |
| **Tooling** | Prettier · `.editorconfig` |

---

## 📁 Project Structure

The folder layout mirrors the backend's bounded contexts to keep the system consistent, modular, and easy to onboard.

```
UI-NG/
 ├── public/                     # Static assets copied as-is
 │    ├── images/                # avatar + galleria/ (18 landing-page photos)
 │    └── media/                 # fpfl-user-guide.pdf, financial-planner-guide.pdf
 ├── src/
 │    ├── app/
 │    │    ├── core/             # App-wide building blocks
 │    │    │    ├── api/         # ApiResponse<T> envelope, ApiError, apiUrl()
 │    │    │    ├── auth/        # AuthService, AuthUi, guards, models
 │    │    │    │    └── ui/     # login / register / change-password / session-expiring dialogs
 │    │    │    ├── http/        # loading, error and auth interceptors
 │    │    │    ├── layout/      # topbar, footer, sidebar
 │    │    │    └── system/      # not-found, error pages
 │    │    ├── features/         # Lazy-loaded pages, one folder per domain
 │    │    │    ├── home/        # Galleria landing page
 │    │    │    ├── item-types/  # list + admin add / edit / delete
 │    │    │    ├── time-periods/# list + admin add / edit / delete
 │    │    │    ├── item-detail/ # credits & debits (list, period picker, edit) + initial amount
 │    │    │    ├── accounts/    # Cards & Loans: account cards, add / edit dialog, account page with purchases
 │    │    │    ├── debt-paydown/# tracked debts and the credits allocated to them
 │    │    │    ├── display/     # forecast: date range, summary, chart, ledger, cards & loans and paydown tabs, Excel export
 │    │    │    ├── admin/       # System Status page (admin): UI checks + API /health and /info
 │    │    │    └── docs/        # Docs page: overview, architecture, ledger engine, data model, CI/CD
 │    │    ├── app.config.ts     # Providers (replaces AppModule)
 │    │    ├── app.routes.ts     # Top-level routes
 │    │    └── app.ts / .html    # Application shell
 │    ├── environments/          # environment.ts (prod) / environment.development.ts
 │    ├── testing/               # Shared test providers and helpers
 │    ├── main.ts                # bootstrapApplication(App, appConfig)
 │    └── styles.scss            # Global styles and design tokens
 ├── Dockerfile                  # Multi-stage build → unprivileged nginx
 ├── nginx.conf                  # SPA fallback, caching, /healthz
 ├── docker-compose.yml          # Local V3 container run (UI at localhost:8200, API at localhost:8300)
 ├── docker-compose.prod.yml     # Image pointed at the public API
 ├── angular.json
 └── package.json
```

Each feature keeps its own `data-access/` service, model, component, template, styles, and spec side by side.

---

## ⚙️ Configuration

Angular environment files are swapped at build time by `fileReplacements` in [`angular.json`](angular.json):

| File | Used by | API URL |
| ---- | ------- | ------- |
| [`environment.development.ts`](src/environments/environment.development.ts) | `ng serve`, development builds | `http://localhost:5132` |
| [`environment.ts`](src/environments/environment.ts) | Production builds | `https://api-net.ledger-finance.com` |

| Key | Purpose |
| --- | ------- |
| `production` | Production flag |
| `apiUrl` | API base URL used by `apiUrl()` and to decide which requests get a bearer token |
| `primeuiLicense` | Free PrimeUI Community license key passed to `providePrimeNG({ license })` |

> ⚠️ Environment values are inlined **at build time**. Changing the API URL means rebuilding — the [`Dockerfile`](Dockerfile) takes an `API_URL` build arg and writes it into `environment.ts` before `ng build`, and the CI workflow passes the production URL. The PrimeUI license key ships in the bundle by design; it is not a secret.

---

## 🔌 HTTP Layer

All requests go through `HttpClient`, configured in [`app.config.ts`](src/app/app.config.ts) with three functional interceptors (outermost first):

1. [`loadingInterceptor`](src/app/core/http/loading-interceptor.ts) — drives the global progress bar through the [`LoadingService`](src/app/core/http/loading.ts) signal
2. [`errorInterceptor`](src/app/core/http/error-interceptor.ts) — turns every failure into an [`ApiError`](src/app/core/api/api-response.ts) `{ status, message, correlationId, url }`, reading the API's envelope when present, so components never handle a raw `HttpErrorResponse`
3. [`authInterceptor`](src/app/core/http/auth-interceptor.ts) — adds `Authorization: Bearer <accessToken>` to API URLs only (never to third parties), and on a 401 performs one shared token refresh and retries; if the refresh fails the session ends with an "expired" notice

Every API response is typed as [`ApiResponse<T>`](src/app/core/api/api-response.ts) — `{ status, message, data, correlationId, timestamp }`, the same envelope API‑NET and API‑JDK return. Error screens show the `correlationId` as a reference that matches the server log.

Features read data with **`httpResource`** inside a small `data-access` service (for example [`ItemTypesApi`](src/app/features/item-types/data-access/item-types-api.ts)), which replaces the original version's NgRx actions, effects, and reducers.

---

## 🔐 Authentication & Session

- [`AuthService`](src/app/core/auth/auth.ts) holds the session in a signal and exposes `isAuthenticated`, `isGuest`, `isAdmin`, `displayName`, and `accessToken` as computed signals
- Login, guest login, register, refresh, change password, and logout map one‑to‑one onto API‑NET's `/auth/*` endpoints; **guest login sends no password** — the server signs the guest in
- The session is kept in `sessionStorage` (per tab); on startup `provideAppInitializer` restores it and refreshes an access token that is about to expire
- Concurrent 401s share a **single in‑flight refresh**, and each refresh token is single‑use
- [`AuthUi`](src/app/core/auth/auth-ui.ts) coordinates the [login](src/app/core/auth/ui/login-dialog/login-dialog.ts), [register](src/app/core/auth/ui/register-dialog/register-dialog.ts), and [change‑password](src/app/core/auth/ui/change-password-dialog/change-password-dialog.ts) dialogs, return‑URL handling, and toasts
- [`SessionExpiringDialog`](src/app/core/auth/ui/session-expiring-dialog/session-expiring-dialog.ts) warns **two minutes** before the access token expires and offers **Stay logged in** (refresh) or **Logout**
- [`authGuard`](src/app/core/auth/auth-guard.ts) protects signed‑in routes — signed‑out visitors go home with the login dialog open and return to the requested page after signing in; [`adminGuard`](src/app/core/auth/admin-guard.ts) is ready for admin‑only pages
- Change Password is hidden for the guest account, and admin‑only actions (add / edit / delete) are shown only to `ROLE_ADMIN` — the API enforces the same rules, so the UI is a convenience rather than the only defense

---

## 🗺️ Routes

| Path | Access | Page |
| ---- | ------ | ---- |
| `/` | Public | Home — Galleria slideshow, overview, guide PDF, sign‑in options |
| `/item-types` | Authenticated | Item types list · **admin**: add, edit (name and id), delete |
| `/time-periods` | Authenticated | Time periods list · **admin**: add, edit (name and id), delete |
| `/initial-amount` | Authenticated | Starting balance — view and edit inline |
| `/credits` · `/debits` | Authenticated | Your credits / debits — filter, sort, edit, delete |
| `/credits/new` · `/debits/new` | Authenticated | Pick a time period for a new item |
| `/credits/new/:periodId` · `/debits/new/:periodId` | Authenticated | New item for that period |
| `/credits/:id/edit` · `/debits/:id/edit` | Authenticated | Edit an item |
| `/accounts` | Authenticated | Cards & Loans — your credit cards and loans, what's owed, and which credits and debits pay them |
| `/accounts/:id` | Authenticated | One card or loan — its details and payments, and its purchases to add, edit and delete |
| `/debt-paydown` | Authenticated | Debt Paydown — track debts, set balances / APR, allocate credits |
| `/display` | Authenticated | The forecast — summary, chart, ledger, cards & loans chart / ledger, paydown chart / ledger, Excel export |
| `/docs` | Public | Documentation — overview, architecture, ledger engine, data model, CI/CD; links to the user guide, Swagger and GitHub |
| `/status` | **Admin** | System Status — UI checks, the API's `/health` components and `/info`, 30‑second auto refresh, raw responses |
| `**` | Public | Not found |

Every route is lazy‑loaded with `loadComponent`, and route `data` / params reach components as inputs (`withComponentInputBinding`) — the credit and debit pages are one set of components told which item type they show.

---

## 📦 Features

Each feature folder under `src/app/features` encapsulates:

- A `data-access/` service (`httpResource` reads, `HttpClient` writes)
- A model interface matching the API DTO
- A standalone `OnPush` component with its template, styles, and spec

Implemented today:

- **`home`** — PrimeNG Galleria auto‑cycling through landing photos, the project summary, the FPFL User Guide PDF, and guest / sign‑in calls to action
- **`item-types`** and **`time-periods`** — PrimeNG tables with refresh, error messages with correlation reference, and an admin dialog for add / edit / delete whose name validation matches the API's strict sanitizer
- **Sidebar** ([`core/layout/sidebar`](src/app/core/layout/sidebar)) — a ☰ drawer from the top bar; the menu is a small data file ([`menu.ts`](src/app/core/layout/sidebar/menu.ts)) with a role filter, so the Admin section only appears for `ROLE_ADMIN`
- **`item-detail`** — credits and debits:
  - **List** — sortable, paged table with a filter box, a readable schedule for each item ("Every other Friday", "On the 1st and 15th"), and delete with confirmation
  - **Period picker** — ten cards, one per time period, each with an example
  - **Edit** — one reactive form for all ten periods; choosing a period shows only its fields, and the same rules as the API's `ItemRules` run before saving
  - **Initial amount** — the starting balance as a card with inline edit; the API creates $0.00 automatically, so there's no "create" step
- **`accounts`** — Cards & Loans *(V3)*:
  - **Cards** per credit card or loan: what's owed (as of a date, with APR and any credit limit), its purchases, and which credits and debits pay it
  - **Editor** ([`AccountEditor`](src/app/features/accounts/account-editor/account-editor.ts)) — a dialog for the account's details and its payments: each credit sends a fixed amount or a percentage, each debit its whole amount; the same rules as the API's `CreditAccountRules` run before saving
  - **Account page** ([`AccountDetail`](src/app/features/accounts/account-detail/account-detail.ts), `/accounts/:id`) — the account's details and payments, plus its purchases (One Time, Daily or Weekly) to add, edit and delete
- **`debt-paydown`** — Debt Paydown setup:
  - **Cards** per tracked debt: its payment and schedule, what's owed (as of a date, with APR), and each allocation ("70% of Paycheck · $1,400.00 each")
  - **Editor** — choose an untracked debit, enter balance / as‑of date / APR, add allocations; each row shows the dollars per occurrence live and how much of that credit other debts already take
  - The same rules as the API's `DebtRules` run before saving, so "Paycheck is already 70% allocated…" shows without a round trip
- **`docs`** — the documentation page (UI-TSX's DocsPage): what the system does, where things live, the UI and API stacks, how a forecast and a paydown are computed, the data model and the pipeline, in tabs; linked from the home page, the top bar and the menu
- **`admin/status`** — System Status for admins (UI-TSX's StatusPage): the UI's own checks (rendering, connection, session, router, API URL, build, uptime) and the API's `/health` components (`db`, `diskSpace`, `ping`) with latency, plus version, commit, build time, runtime and uptime from `/info`. Probes use `fetch` with a 5‑second timeout and say *why* a check failed (unreachable, timed out, or answered but blocked by CORS). Auto refresh every 30 seconds can be paused; the raw responses are one click away
- **`display`** — the forecast:
  - **Date range** with quick ranges (30 days → 5 years); runs on arrival with today + 90 days
  - **Summary tiles** — starting, credits, debits, lowest (with date) and ending balance, plus a warning when the balance drops below $0
  - **Chart** — the balance over time, and each day's credits above zero / debits below; colors checked for color‑blind readers
  - **Ledger** — one row per day with expandable items, an "Only days with activity" switch, and expand / collapse all
  - **Cards & loans chart / Cards & loans ledger** — shown when there are cards or loans: a card per account (owed, purchases, paid, interest, payoff date), every balance over time, and every purchase, payment and interest charge with the balance after it. Money sent from credits also shows on the main ledger as "Card/Loan Payment" debits
  - **Paydown chart / Paydown ledger** — shown when debts are tracked: a card per debt (owed, paid, interest, payoff date), every debt's balance as a stepped line in its own fixed color, and every payment, allocation and interest charge with the balance left, filterable by debt
  - **Export to Excel** — a port of the original Angular app's export (plus a **Cards & Loans** sheet when there are cards or loans, and a **Debt Paydown** sheet when debts are tracked): the same ledger as a workbook, each day's items in a collapsed outline group that Excel's +/– buttons open, with real dates and accounting‑formatted amounts

---

## 🚧 Project Status

The goal is the same feature set as [UI-TSX](https://github.com/rdonalson/FPFL-React-Java/tree/main/UI-TSX).

| Area | Status |
| ---- | ------ |
| App shell — top bar, footer, progress bar, toasts | ✅ Done |
| Landing page — Galleria slideshow, guide PDF | ✅ Done |
| Login, guest login, register, change password, logout | ✅ Done |
| Silent token refresh, session‑expiring dialog | ✅ Done |
| Item types — list + admin add / edit / delete | ✅ Done |
| Time periods — list + admin add / edit / delete | ✅ Done |
| Docker image, AKS deployment, CI/CD | ✅ Done |
| Sidebar menu with role‑filtered admin section | ✅ Done |
| Credit / debit items with per‑period add / edit pages | ✅ Done |
| Initial amount | ✅ Done |
| Forecasted ledger table + chart with date‑range selection | ✅ Done |
| Ledger download to Excel with expandable rows | ✅ Done (not in UI-TSX) |
| Cards & Loans — accounts page, account page with purchases, chart / ledger tabs, Excel sheet *(V3)* | ✅ Done (not in UI-TSX) |
| Debt Paydown — setup page, paydown chart / ledger tabs, Excel sheet | ✅ Done (not in UI-TSX) |
| Docs page | ✅ Done |
| Admin status page (UI + API health) | ✅ Done |
| Client error forwarding to `/client-logs` | 🔜 Planned |

---

## 🧪 Running the App

```bash
npm install
npm start
```

The dev server runs on **<http://localhost:4200>** and calls the API at `http://localhost:5132`. Requires **Node 24.15+**.

### Scripts

| Script | Purpose |
| ------ | ------- |
| `npm start` | `ng serve` on port 4200 |
| `npm run build` | Production build to `dist/ui-ng/browser` |
| `npm run watch` | Development build in watch mode |
| `npm test` | Vitest unit tests (jsdom) |

---

## 🧪 Testing

Unit tests run on **Vitest** through Angular's `@angular/build:unit-test` builder. The suite covers the auth service, the auth and error interceptors, guards, the shell, layout, sidebar menu, and every feature page, using the real interceptors against `HttpTestingController`. Logic lives in plain functions where it can — item form validation, schedule descriptions, ledger summaries, chart data, and the Excel workbook builder — so most of it is tested without rendering anything.

Shared setup lives in [`src/testing/test-providers.ts`](src/testing/test-providers.ts) (`appTestProviders()`, `envelope()`, `authResponse()`).

```bash
npm test
```

---

## 🐳 Docker

[`Dockerfile`](Dockerfile) is a two‑stage build: `node:24-alpine` runs `npm ci`, writes the `API_URL` build arg into `environment.ts`, and runs `ng build --configuration production`; the runtime stage serves `dist/ui-ng/browser` from **`nginxinc/nginx-unprivileged`** on port **8080** as a non‑root user.

[`nginx.conf`](nginx.conf) adds SPA fallback (`try_files … /index.html`), one‑year caching for hashed bundles, `no-cache` for `index.html`, gzip, basic security headers, and a `/healthz` probe endpoint.

```bash
docker compose up --build                              # UI at http://localhost:8200, calling the V3 API at http://localhost:8300
docker compose -f docker-compose.prod.yml up --build   # pointed at https://api-net.ledger-finance.com
```

---

## 🔄 CI/CD

Pushes to `master` touching `UI-NG/**` or `k8s/ui-ng/**` trigger [`build-push-ui-ng.yml`](../.github/workflows/build-push-ui-ng.yml):

1. Build and push `fpflacr.azurecr.io/fpfluingimg` tagged `:${{ github.sha }}` and `:latest`, passing `API_URL` as a build arg
2. Azure login → set AKS context (`fpfl-cluster`) → `kubectl apply` the UI service, deployment, and ingress
3. `kubectl rollout restart deployment/ui-ng -n fpfl` and wait for `rollout status`

The pod runs on the dedicated `netpool` node pool behind the nginx ingress at `ui-ng.ledger-finance.com`. Full pipeline details are in the [root README](../README.md#-cicd-pipeline).

---

## 🧰 Development Notes

- No NgModules — every component, directive, and pipe is standalone and imports what it uses
- Prefer signals and `computed` over RxJS subjects for component state; use `httpResource` for reads
- Never call `HttpClient` from a component; go through the feature's `data-access` service so the interceptors and `ApiResponse<T>` typing apply
- Use PrimeNG template references (`#header`, `#body`, `#item`, `#emptymessage`) rather than `pTemplate`
- Keep component styles within the `anyComponentStyle` budget in `angular.json`; shared styles belong in `styles.scss`
- Never remove or hide PrimeNG's license check — the key belongs in `environment.ts`
- `httpResource.value()` throws while the resource is in an error state — read it behind `hasValue()`
- jsdom has no `ResizeObserver` or canvas; specs that render PrimeNG tabs or charts stub them (see [`display.spec.ts`](src/app/features/display/display.spec.ts))
- Heavy, rarely used libraries are loaded with `import()` at the point of use (ExcelJS), keeping the initial bundle small

---

## 📄 License

This project is licensed under the [MIT License](../LICENSE).
