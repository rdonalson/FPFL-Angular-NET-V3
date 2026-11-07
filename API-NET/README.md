# Financial Planner: Forecast Ledger — API NET

[![Build](https://github.com/rdonalson/FPFL-Angular-NET/actions/workflows/build-push-api-net.yml/badge.svg)](https://github.com/rdonalson/FPFL-Angular-NET/actions/workflows/build-push-api-net.yml)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-10-512BD4)](https://learn.microsoft.com/aspnet/core)
[![License](https://img.shields.io/badge/license-MIT-lightgrey)](../LICENSE)

A clean, enterprise‑grade, modular .NET platform built with **.NET 10**, **ASP.NET Core**, and **Entity Framework Core**. The solution follows Domain‑Driven Design and is organized into bounded‑context projects that mirror the Maven modules of the sister [API‑JDK](https://github.com/rdonalson/FPFL-React-Java/tree/main/API-JDK), for clarity, testability, and long‑term maintainability.

It replaces the original version's .NET Core 3.1 layered API, bringing over API‑JDK's response envelope, centralized error handling, sanitizers, and JWT auth with refresh‑token rotation.

> ✅ **At parity with API‑JDK, and past it.** Auth, items, the initial amount, and the forecast ledger are all ported — with ownership checks, exact‑cent math, and recurrence fixes API‑JDK lacks — plus two features of its own: **Cards & Loans** (V3) and **Debt Paydown**. See [Project Status](#-project-status).

---

## Overview

**Purpose**
Provide a modular backend API that stores, manages, and projects user financial planning items (recurring payments, incomes, and scheduled events) into a day‑by‑day forecast ledger. **Fpfl.Api** exposes the REST endpoints and is the single runnable entrypoint; authentication, domain logic, projections, and persistence live in separate bounded‑context projects.

**Key technologies**
.NET 10 · ASP.NET Core (controllers) · Entity Framework Core 10 + Npgsql · JWT bearer (`Microsoft.IdentityModel.JsonWebTokens`) · BCrypt.Net‑Next · `Microsoft.AspNetCore.OpenApi` + Swagger UI · ASP.NET Core rate limiting & health checks · xUnit · PostgreSQL

**Database**
PostgreSQL **FPFL‑V3** — a copy of the FPFL‑V2 database API‑JDK uses, plus the Cards & Loans tables. API‑JDK stays on FPFL‑V2.

**Deployed as**
A multi‑stage Docker image (`fpflapinetimg`) pushed to Azure Container Registry and rolled out to AKS by GitHub Actions. Live at <https://api-net.ledger-finance.com> · Swagger UI at <https://api-net.ledger-finance.com/swagger-ui>.

---

## Quick links

- **Runnable project**: `src/Fpfl.Api/` (ASP.NET Core app, the only executable)
- **Request samples**: [`src/Fpfl.Api/Fpfl.Api.http`](src/Fpfl.Api/Fpfl.Api.http) — runnable from Visual Studio / VS Code
- **Docs**: [`docs/`](docs) — module architecture and ER diagrams
- **CI/CD workflow**: [`../.github/workflows/build-push-api-net.yml`](../.github/workflows/build-push-api-net.yml)
- **Kubernetes manifests**: [`../k8s/api-net`](../k8s/api-net)
- **License**: [`../LICENSE`](../LICENSE) (MIT)

---

## Project structure

Each bounded context is a class‑library project referenced by `Fpfl.Api`, all in one solution ([`Fpfl.slnx`](Fpfl.slnx)).

```
API-NET/
 ├── src/
 │    ├── Fpfl.Api/        # REST API, controllers, DTOs, mappers, envelope, error handling, app bootstrap
 │    ├── Fpfl.Auth/       # Authentication and authorization domain
 │    ├── Fpfl.Items/      # Domain + persistence for items, initial amount, item types, time periods, cards & loans, debts
 │    ├── Fpfl.Display/    # Forecast ledger: recurrence expanders, ledger builder, card / loan and paydown calculators
 │    └── Fpfl.Common/     # Shared exceptions and sanitization
 ├── db/                   # SQL scripts for tables API-NET adds (001 Debt Paydown, 002 Cards & Loans)
 ├── tests/
 │    └── Fpfl.Api.Tests/  # xUnit tests
 ├── docs/                 # Architecture and ER diagrams
 ├── Dockerfile            # Multi-stage build (SDK 10 → ASP.NET 10 runtime)
 ├── docker-compose.yml    # Local container run
 └── Fpfl.slnx             # Solution
```

Inside each bounded context the folders follow the same layering: `Domain/` (entities, repository and service interfaces) → `Application/` (services) → `Infrastructure/` (EF Core `DbContext`, configurations, repositories, health checks), plus a `DependencyInjection.cs` that registers the context with one call (`AddFpflItems`, `AddFpflDisplay`, `AddFpflAuth`, `AddFpflCommon`).

---

## Module responsibilities

### **Fpfl.Api** (mirrors `module-api`)

- Exposes REST endpoints and the OpenAPI document (`/openapi/v1.json`), browsable in Swagger UI at `/swagger-ui`
- Defines DTOs (records with validation attributes), explicit mappers, and the `ApiResponse<T>` envelope
- `ApiResponseFactory` runs every outgoing payload through the sanitizer
- `GlobalExceptionHandler` (`IExceptionHandler`) maps domain exceptions to HTTP statuses, logs with a **correlation ID**, and returns a generic message for 500s
- `InvalidModelStateResponseFactory` returns validation failures in the same envelope; `AuthEnvelopeEvents` does the same for JWT 401/403s
- Owns the composition root: CORS, authentication/authorization, rate limiting, health checks, static landing page

**Controllers:** `AuthController`, `UserRolesController`, `ItemsController`, `InitialAmountController`, `AccountsController`, `DebtsController`, `DisplayController`, `ItemTypesController`, `TimePeriodsController`, `ClientLogsController`

`AuthController`'s register also creates the new user's $0.00 initial amount (best effort — a failure there never fails the sign‑up).

### **Fpfl.Auth** (mirrors `module-auth`)

- Users, roles, user‑role assignments, and persisted refresh tokens (`AuthDbContext`)
- `AuthService` — login, guest login, register, refresh (rotation), logout, change password
- `UserRolesService` — admin role management, with a guard that stops an admin removing their own admin role
- `JwtTokenService` — HS256 access tokens, random refresh tokens stored as SHA‑256 hashes
- `BCryptPasswordHasher` — work factor 10, verifies the Spring `$2a$` hashes already in the shared database
- `AuthOptions` (`Auth` config section) — issuer, audience, signing key (≥ 32 chars, fail‑fast at startup), token lifetimes, guest account
- Registers JWT bearer authentication with `MapInboundClaims = false` so claim names stay as issued (`sub`, `id`, `user_id`, `role`, `guest`)

### **Fpfl.Items** (mirrors `module-items-bc`)

- Owns the domain model and invariants for `Item`, `ItemType`, and `TimePeriod`
- `ItemRules` — every rule an item must meet in one place (instead of ten UI forms, as in UI‑TSX): name 1–150 characters, amount above 0 and rounded to cents, credit or debit only, and the right fields for its time period (real month/day pairs, weekdays 0–6, nth index 1–4 or −1). It returns a clean copy with only that period's fields set, because the ledger reads exactly that set
- `ItemService` — a user reads and changes only their own items (someone else's answers 404 so ids can't be probed; admins reach all); the owner always comes from the access token, never the body
- `CreditAccountService` + `CreditAccountRules` — Cards & Loans: a credit card or loan with an opening balance as of a date, APR 0–100% and (cards only) a credit limit; names unique per user; up to 20 payments, each from one of your credits (a fixed amount up to the credit's own amount, or a percentage, with one credit's percentages capped at 100% across your accounts) or a debit (its whole amount, paying only one account); purchases One Time, Daily or Weekly with an optional date range. Someone else's account answers 404
- `DebtService` + `DebtRules` — Debt Paydown's tracked debts: only your own debits (each tracked once) and credits, balance $0–$1B, APR 0–100%, up to 20 allocations, and no credit more than 100% allocated across all your debts; someone else's debt answers 404
- `InitialAmountService` — one starting balance per user (item type 3), created at $0.00 on registration or on first read, so the GET never 404s; amounts must be $0 or more
- Ports‑and‑adapters split: domain `I*Repository` / `I*Service` interfaces, application services, EF Core repositories
- Repositories translate PostgreSQL errors into domain exceptions — unique violations → `DuplicateItemException` (409), foreign‑key violations → `DomainValidationException` (400), anything else → `RepositoryException` (500)
- Supports changing a reference row's **id** as well as its name, refusing the change while items still use it
- `ItemsDatabaseHealthCheck` backs the `db` entry in `/health`

### **Fpfl.Display** (mirrors `module-display-bc`)

- Ten `IRecurrenceExpander`s, one per time period (one‑time, daily, weekly, every two weeks, bi‑monthly, monthly, quarterly, semi‑annual, annual, nth weekday), turn an item into its occurrence dates; `RecurrenceEngine` routes each item to its expander once, and `RecurrenceWindow` narrows the dates to the item's own date range
- `LedgerBuilder` is pure (no database, no clock): one row per day with credit and debit sums, net, and a running balance starting from the initial amount, with that day's entries nested — sums are `decimal` so cents stay exact
- `PaydownCalculator` projects each tracked debt: from the entered balance, every occurrence of the debit's own payment and of each allocated credit (its percentage) reduces what's owed, interest (APR ÷ 12) is added at month ends, payments stop at $0, and it looks up to 30 years ahead for the payoff date. Allocations become `LedgerAdjustment`s — extra "Debt Paydown" debits in the main ledger
- `AccountLedgerCalculator` runs each card or loan day by day from its balance date: purchases raise the balance (never touching the bank), each occurrence of a paying credit sends its fixed amount or percentage and each paying debit its whole amount, payments are cut to what's owed, interest (APR ÷ 12) is added at month ends, and it looks up to 30 years ahead for the payoff date. Money sent from credits becomes `LedgerAdjustment`s — "Card/Loan Payment" debits in the main ledger; a debit is already there, so it adds none
- `LedgerReadoutService` loads the user's items, cards and loans, and tracked debts, enforces ownership and a 10‑year cap, and hands them to the calculator and the builder
- Replaces the original version's database stored procedure with tested application code, and fixes three API‑JDK bugs on the way: Sunday items threw (`DayOfWeek.of(0)`), every‑two‑weeks dates shifted with the ledger's start date, and any user's ledger could be requested by id

### **Fpfl.Common** (mirrors `module-common-bc`)

- Shared exceptions: the abstract `DomainException` base, plus `DomainValidationException` (400), `SanitizationException` (400), `InvalidCredentialsException` (401), `ForbiddenOperationException` (403), `ItemNotFoundException` (404), `DuplicateItemException` (409), `RepositoryException` (500)
- `Sanitizer` — port of API‑JDK's `SanitizerImpl`: walks object graphs and normalizes every string (BOM, zero‑width, and control characters stripped; whitespace collapsed), then enforces **strict** text (letters, digits, space, `-_'.`) by default, **lenient** text with `[LenientText]`, or a URI character set; `INoSanitize` opts a type out
- Use sparingly — only for true cross‑cutting concerns

---

## Dependency rules

- No cross‑context leakage: domain logic stays in its owning bounded context.
- No circular dependencies.
- `Fpfl.Api` is the only runnable project and the only one that knows about HTTP contracts (DTOs, envelope).
- Each bounded context owns its own `DbContext` mapped onto the shared schema.
- Actual dependency edges:

```
Fpfl.Api  ──► Fpfl.Auth ──► Fpfl.Common
    │                          ▲
    ├──────► Fpfl.Display ─────┤
    │              │           │
    │              ▼           │
    └──────► Fpfl.Items ───────┘
```

---

## 🧭 Architecture Overview

The module layout is the same as API‑JDK's — an API layer delegating to Auth, Items, and Display contexts over shared common utilities and one PostgreSQL database:

![Modular Dependency Diagram](docs/FPFL-Module-Architecture.png)

---

## 🌐 Endpoints

Routes and response messages match API‑JDK. Every response uses the envelope:

```json
{ "status": 200, "message": "ItemTypes fetched successfully", "data": [ ... ], "correlationId": "", "timestamp": "2026-10-17T16:00:00Z" }
```

Errors carry `"data": null` and a `correlationId` that matches the server log entry.

| Method & route | Access | Purpose |
| -------------- | ------ | ------- |
| `POST /auth/login` | Public | Sign in with email + password |
| `POST /auth/guest` | Public | Sign in as the shared guest account (no password sent) |
| `POST /auth/register` | Public | Create an account (`ROLE_USER`) and sign it in |
| `POST /auth/refresh` | Public | Exchange a refresh token for a new session (single‑use) |
| `POST /auth/logout` | Public | Revoke a refresh token |
| `POST /auth/change-password` | Signed in (not guest) | Change password; signs out other devices |
| `GET /auth/me` | Signed in | Who the access token belongs to |
| `GET /items` | **Admin** | Every item |
| `GET /items/{id}` | Signed in | One of your items (someone else's → 404) |
| `GET /items/{userId}/{itemType}` | Signed in | Your credits (`1`) or debits (`2`) |
| `POST /items` · `PUT /items/{id}` · `DELETE /items/{id}` | Signed in | Create / update / delete your item (validated by `ItemRules`) |
| `GET /initial-amount/{userId}` | Signed in | Your starting balance — created at $0.00 if missing |
| `POST /initial-amount` · `PUT /initial-amount/{id}` | Signed in | Create / change your starting balance |
| `GET /debts` · `GET /debts/{id}` | Signed in | Your tracked debts with their allocations |
| `POST /debts` · `PUT /debts/{id}` · `DELETE /debts/{id}` | Signed in | Track a debit / change its balance, APR, date and allocations / stop tracking it |
| `GET /accounts` · `GET /accounts/{id}` | Signed in | Your credit cards and loans with their purchases and payments |
| `POST /accounts` · `PUT /accounts/{id}` · `DELETE /accounts/{id}` | Signed in | Add / change (details and the payments, which are replaced as a set) / delete a card or loan |
| `GET /accounts/{id}/purchases` | Signed in | An account's purchases |
| `POST /accounts/{id}/purchases` · `PUT …/purchases/{purchaseId}` · `DELETE …/purchases/{purchaseId}` | Signed in | Add / change / delete a purchase (One Time, Daily or Weekly) |
| `POST /display/ledger` | Signed in | The forecast: one row per day for `ledgerStartDate`–`ledgerEndDate` (≤ 10 years), including "Debt Paydown" and "Card/Loan Payment" debits |
| `POST /display/paydown` | Signed in | Same body: each tracked debt's daily balances, payments, allocations, interest, totals and payoff date |
| `POST /display/accounts` | Signed in | Same body: each card or loan's balance per day, its purchases, payments and interest, totals and payoff date |
| `GET /item-types` · `GET /item-types/{id}` | Public | List / get item types |
| `POST /item-types` · `PUT /item-types/{id}` · `DELETE /item-types/{id}` | **Admin** | Create / rename or change id / delete |
| `GET /time-periods` · `GET /time-periods/{id}` | Public | List / get time periods |
| `POST /time-periods` · `PUT /time-periods/{id}` · `DELETE /time-periods/{id}` | **Admin** | Create / rename or change id / delete |
| `GET /users/{userId}/roles` | **Admin** | A user's role names |
| `POST /users/{userId}/roles/{roleId}` · `DELETE …/{roleId}` | **Admin** | Add / remove one role |
| `PUT /users/{userId}/roles` | **Admin** | Replace all roles (`{ "roleIds": [...] }`) |
| `POST /client-logs` | Public, rate‑limited | Write a UI log entry to the server log |
| `GET /health` | Public | Health in Actuator shape: `db` (PostgreSQL reachable), `diskSpace` (free space above 10 MB) and `ping`; 503 when any is DOWN |
| `GET /info` | Public | Actuator‑style info: `app` (name, version), `build` (version, commit, build time) and `runtime` (.NET version, OS, environment, uptime) |

`PUT /item-types/{id}` and `PUT /time-periods/{id}` accept an optional `id` in the body: omit it to rename (as API‑JDK does), or send a different value to change the id.

The ledger request and response keep API‑JDK's shape, so UI‑TSX's types work unchanged (`userId` is optional and defaults to the caller; naming someone else needs the admin role):

```json
// POST /display/ledger
{ "ledgerStartDate": "2026-10-17", "ledgerEndDate": "2027-01-15", "includeGrouping": false }

// one day of the response's data[]
{ "rollupKey": 2, "year": 2026, "wDate": "2026-10-18",
  "creditSummary": 2500, "debitSummary": -2200, "net": 300, "runningTotal": 1300,
  "items": [ { "itemKey": 1, "itemId": 12, "occurrenceDate": "2026-10-18", "fkItemType": 1,
               "itemType": "Credit", "period": "Every Two Weeks", "name": "Paycheck", "amount": 2500 } ] }
```

---

# 🗄️ PostgreSQL Database Structure

API‑NET V3 connects to the **FPFL‑V3** database: a copy of FPFL‑V2, the database API‑JDK uses, with the Cards & Loans tables added. The `fpfl` schema itself comes from API‑JDK (Hibernate `ddl-auto: update`), so EF Core maps onto the existing tables and is **never** used to generate migrations against it; tables only API‑NET uses are created by the numbered, re‑runnable scripts in [`db/`](db).

| Database | Used by | Servers |
| -------- | ------- | ------- |
| **FPFL‑V3** | API‑NET (V3) | Local PostgreSQL and Azure `pg-fpfl-v2.postgres.database.azure.com` |
| FPFL‑V2 | API‑JDK | Same servers |

---

## **📌 Tables Overview**

### **items**

Stores all user‑defined financial items — credits, debits, and each user's initial amount (item type 3) — including scheduling metadata. Mapped by `ItemConfiguration`. The recurrence columns are mutually exclusive — only the set matching the item's `fk_time_period` is populated, and the ledger reads exactly that set when expanding occurrences.

| **Column** | **Description** |
| ---------- | --------------- |
| **id** | Primary key |
| **user_id** | Owner of the item (UUID referencing `users.user_id`) |
| **name** / **amount** | Item name and monetary value |
| **fk_item_type** | Foreign key → `item_types.id` |
| **fk_time_period** | Foreign key → `time_periods.id` |
| **begin_date** / **end_date** | Date bounds |
| **weekly_dow** / **every_other_week_dow** | Weekly and every‑two‑weeks day of week (0 = Sunday … 6 = Saturday) |
| **bi_monthly_day_1** / **bi_monthly_day_2** | Bi‑monthly days |
| **monthly_dom** | Monthly day of month |
| **quarterly_1..4_month** / **quarterly_1..4_day** | Quarterly dates |
| **semi_annual_1..2_month** / **semi_annual_1..2_day** | Semi‑annual dates |
| **annual_moy** / **annual_dom** | Annual month and day |
| **nth_dow** / **nth_index** | Nth‑weekday pattern (index 1–4, or −1 for last) |
| **date_range_req** | Whether a date range is required |

---

### **credit_accounts** · **account_purchases** · **account_payments** *(API‑NET only — [`db/002_credit_accounts.sql`](db/002_credit_accounts.sql))*

Cards & Loans' tables. Deleting an account deletes its purchases and payments; deleting a credit or debit deletes its payments (`ON DELETE CASCADE`).

| **Table** | **Columns** |
| --------- | ----------- |
| **credit_accounts** | `id`, `user_id`, `name` (unique per user), `kind` (`CreditCard` / `Loan`), `opening_balance` numeric(14,2) ≥ 0, `balance_date`, `apr` numeric(6,3) 0–100, `credit_limit` numeric(14,2) (optional), `created_at` |
| **account_purchases** | `id`, `fk_account` → `credit_accounts.id`, `name`, `amount` numeric(14,2) > 0, `fk_time_period` → `time_periods.id` (1 One Time, 2 Daily, 3 Weekly), `begin_date`, `end_date`, `weekly_dow` 0–6, `date_range_req`, `created_at` |
| **account_payments** | `id`, `fk_account` → `credit_accounts.id`, `fk_credit_item` → `items.id` (the paying credit or debit), `amount` numeric(14,2) **or** `percent` numeric(5,2) — exactly one is set; unique per (account, item) |

---

### **debts** · **debt_allocations** *(API‑NET only — [`db/001_debt_paydown.sql`](db/001_debt_paydown.sql))*

Debt Paydown's tables. API‑JDK doesn't know them, and Hibernate's `update` mode leaves them alone. Deleting a debit deletes its debt; deleting a credit deletes its allocations (`ON DELETE CASCADE`).

| **Table** | **Columns** |
| --------- | ----------- |
| **debts** | `id`, `user_id`, `fk_item` → `items.id` (the debit, unique), `balance` numeric(14,2) ≥ 0, `apr` numeric(6,3) 0–100, `balance_date`, `created_at` |
| **debt_allocations** | `id`, `fk_debt` → `debts.id`, `fk_credit_item` → `items.id`, `percent` numeric(5,2) in (0, 100]; unique per (debt, credit) |

---

### **item_types**

The credit / debit / initial‑amount classification applied to the ledger. Mapped by `ItemTypeConfiguration`; the id is supplied by the caller (not generated).

| **Column** | **Description** |
| ---------- | --------------- |
| **id** | Primary key |
| **name** | Type name |

---

### **time_periods**

The recurrence period for an item; the chosen period determines which recurrence columns on `items` are used. Mapped by `TimePeriodConfiguration`; the id is supplied by the caller because the recurrence expanders are keyed by it.

| **Column** | **Description** |
| ---------- | --------------- |
| **id** | Primary key |
| **name** | Period name (one‑time, daily, weekly, monthly, quarterly, annual, …) |

---

### **users**

Application accounts used by `Fpfl.Auth` and referenced by items.

| **Column** | **Description** |
| ---------- | --------------- |
| **id** | Primary key (identity) used for relational joins |
| **user_id** | Application UUID for the user (used as `items.user_id`) |
| **email** | Unique sign‑in name (matched case‑insensitively) |
| **password_hash** | BCrypt hash |
| **first** / **last** | First and last name |
| **created_at** | Account creation timestamp |

---

### **roles** · **user_roles**

| **Table** | **Columns** |
| --------- | ----------- |
| **roles** | `id` (identity), `name` (`ROLE_USER`, `ROLE_ADMIN`) |
| **user_roles** | `user_id` → `users.id`, `role_id` → `roles.id` (composite key) |

---

### **refresh_tokens**

| **Column** | **Description** |
| ---------- | --------------- |
| **id** | Primary key (identity) |
| **token** | **SHA‑256 hash** of the refresh token — the raw token is never stored |
| **user_id** | FK → `users.id` |
| **expiry_date** | Expiration timestamp |

---

## **🔗 Relationships**

- `items.fk_item_type` → `item_types.id` · `items.fk_time_period` → `time_periods.id` · `items.user_id` → `users.user_id`
- `credit_accounts.user_id` → the owner's `users.user_id` · `account_purchases.fk_account` → `credit_accounts.id` · `account_purchases.fk_time_period` → `time_periods.id` · `account_payments.fk_account` → `credit_accounts.id` · `account_payments.fk_credit_item` → `items.id`
- `debts.fk_item` → `items.id` · `debt_allocations.fk_debt` → `debts.id` · `debt_allocations.fk_credit_item` → `items.id`
- `user_roles.user_id` → `users.id` · `user_roles.role_id` → `roles.id`
- `refresh_tokens.user_id` → `users.id`

---

## **🧩 ER Diagram**

The whole FPFL‑V3 database in one diagram. `ItemsDbContext` maps `items` with its lookups `item_types` and `time_periods`, Cards & Loans' `credit_accounts` with their `account_purchases` and `account_payments`, and Debt Paydown's `debts` and `debt_allocations`. `AuthDbContext` maps `users`, `user_roles` joining them to `roles`, and their `refresh_tokens`.

![FPFL-V3 entity relationship diagram](docs/FPFL-ER-V3.png)

The earlier per‑context diagrams, from before Cards & Loans, are still in [`docs/`](docs): [items](docs/FPFL-ER-Item.png) and [auth](docs/FPFL-ER-Auth.png).

---

## ⚙️ Configuration

Non‑secret defaults live in [`appsettings.json`](src/Fpfl.Api/appsettings.json). Secrets come from **user‑secrets** in development and **environment variables** (a Kubernetes secret) in production — never from committed files.

| Key | Default | Purpose |
| --- | ------- | ------- |
| `ConnectionStrings:Fpfl` | *(secret — required)* | Npgsql connection string; startup fails fast if missing |
| `Auth:SigningKey` | *(secret — required, ≥ 32 chars)* | HS256 JWT signing key; startup fails fast if missing or short |
| `Auth:Issuer` / `Auth:Audience` | `fpfl-api-net` / `fpfl-ui-ng` | Token issuer and audience |
| `Auth:AccessTokenMinutes` | `15` | Access token lifetime |
| `Auth:RefreshTokenDays` | `7` | Refresh token lifetime |
| `Auth:GuestEmail` | `guest.user@gmail.com` | Account used by `POST /auth/guest` |
| `Cors:AllowedOrigins` | `["http://localhost:4200"]` | Browser origins allowed to call the API |

Environment variables use double underscores, e.g. `ConnectionStrings__Fpfl`, `Auth__SigningKey`, `Cors__AllowedOrigins__0`.

HTTPS redirection is enabled outside Development only — browsers won't follow a redirect on a CORS preflight, and in production TLS terminates at the ingress (forwarded headers are honored).

---

## Build the entire solution

From the `API-NET` directory:

```bash
dotnet build
```

---

## Run the API

Set the secrets once:

```bash
dotnet user-secrets set "ConnectionStrings:Fpfl" "Host=localhost;Port=5432;Database=FPFL-V3;Username=postgres;Password=..." --project src/Fpfl.Api
dotnet user-secrets set "Auth:SigningKey" "<at least 32 random characters>" --project src/Fpfl.Api
```

Run the scripts in [`db/`](db) once against the local FPFL‑V3 database:

```bash
psql "host=localhost dbname=FPFL-V3 user=postgres" -f db/001_debt_paydown.sql
psql "host=localhost dbname=FPFL-V3 user=postgres" -f db/002_credit_accounts.sql
```

Then run:

```bash
dotnet run --project src/Fpfl.Api
```

Or press **F5** in Visual Studio with the `http` profile.

| Endpoint | URL |
| -------- | --- |
| Welcome page | <http://localhost:5132/> |
| Swagger UI | <http://localhost:5132/swagger-ui> |
| OpenAPI document | <http://localhost:5132/openapi/v1.json> |
| Health | <http://localhost:5132/health> |

---

## 🐳 Docker

The [`Dockerfile`](Dockerfile) is a two‑stage build: `mcr.microsoft.com/dotnet/sdk:10.0` copies the project files first so `dotnet restore` caches cleanly, then publishes `Fpfl.Api`; `mcr.microsoft.com/dotnet/aspnet:10.0` runs it as a **non‑root** user on port **8080** with forwarded headers enabled.

```bash
# copy the values into .env.docker first (connection string, signing key, CORS origins)
docker compose up --build
```

`docker-compose.yml` reads `.env.docker` (Git‑ignored) and maps `8300:8080` (API at <http://localhost:8300>, image `fpflapinetv3img`), so V3 runs beside the earlier version's container on 8100; PostgreSQL on the host is reached through `host.docker.internal`. The root `docker-compose.yml` runs the API on 8100 with the UI.

---

## 🔄 CI/CD

Pushes to `master` that touch `API-NET/**` or `k8s/api-net/**` trigger [`build-push-api-net.yml`](../.github/workflows/build-push-api-net.yml):

1. Run `dotnet test` — a failing test stops the pipeline
2. Build and push `fpflacr.azurecr.io/fpflapinetimg` tagged `:${{ github.sha }}` and `:latest`, with GitHub Actions layer caching
3. Azure login → set AKS context (`fpfl-cluster`) → confirm `api-net-secret` exists → `kubectl apply` the service, deployment, and ingress
4. `kubectl rollout restart deployment/api-net -n fpfl` and wait for `rollout status`

In the cluster the pod runs on the dedicated `netpool` node pool; readiness uses `/health` (database included) and liveness uses `/` so a database outage doesn't restart the pod. Full pipeline details are in the [root README](../README.md#-cicd-pipeline).

---

## 🧪 Running Tests

```bash
dotnet test
```

[`Fpfl.Api.Tests`](tests/Fpfl.Api.Tests) uses **xUnit** with hand‑written in‑memory fakes and a fixed `TimeProvider`, so the suite runs without a database:

- [`AuthServiceTests`](tests/Fpfl.Api.Tests/AuthServiceTests.cs) — login, guest, refresh rotation, expiry, logout, change password, token claims, Spring‑style BCrypt hashes
- [`UserRolesServiceTests`](tests/Fpfl.Api.Tests/UserRolesServiceTests.cs) — add / remove / replace roles, self‑lockout guard
- [`ItemTypeServiceTests`](tests/Fpfl.Api.Tests/ItemTypeServiceTests.cs) · [`TimePeriodServiceTests`](tests/Fpfl.Api.Tests/TimePeriodServiceTests.cs) — validation, duplicates, rename and id changes, delete
- [`SanitizerTests`](tests/Fpfl.Api.Tests/SanitizerTests.cs) — strict / lenient / URI rules, nested objects, opt‑outs
- [`ItemRulesTests`](tests/Fpfl.Api.Tests/ItemRulesTests.cs) — every period's required fields, real dates, rounding, ranges
- [`ItemServiceTests`](tests/Fpfl.Api.Tests/ItemServiceTests.cs) · [`InitialAmountServiceTests`](tests/Fpfl.Api.Tests/InitialAmountServiceTests.cs) — ownership, owner from the token, auto‑created initial amount
- [`RecurrenceEngineTests`](tests/Fpfl.Api.Tests/RecurrenceEngineTests.cs) — each period's dates, month‑end and leap‑year clamping, Sunday, every‑two‑weeks anchoring, item date ranges
- [`LedgerBuilderTests`](tests/Fpfl.Api.Tests/LedgerBuilderTests.cs) — running balance, day ordering, exact cents, ownership and range limits
- [`DisplayMapperTests`](tests/Fpfl.Api.Tests/DisplayMapperTests.cs) — the ledger JSON matches API‑JDK's shape field by field, and survives the response sanitizer with paydown entries
- [`PaydownCalculatorTests`](tests/Fpfl.Api.Tests/PaydownCalculatorTests.cs) — allocations, capped last payment, the debit's own payment, monthly interest, same‑day order, windows before / after the balance date, payoff projection
- [`DebtServiceTests`](tests/Fpfl.Api.Tests/DebtServiceTests.cs) — Debt Paydown rules, the 100%‑per‑credit cap, ownership, and the ledger deducting allocations
- [`AccountLedgerCalculatorTests`](tests/Fpfl.Api.Tests/AccountLedgerCalculatorTests.cs) — Cards & Loans: purchases, fixed and percentage payments from credits, whole‑amount payments from debits, payments cut to what's owed, monthly interest, payoff dates
- [`CreditAccountRulesTests`](tests/Fpfl.Api.Tests/CreditAccountRulesTests.cs) — account fields, kinds and credit limits, payment rules and the per‑credit cap
- [`AccountResponseSanitizerTests`](tests/Fpfl.Api.Tests/AccountResponseSanitizerTests.cs) — account responses pass through the response sanitizer intact

---

## 🚧 Project Status

The goal is the same feature set as [API‑JDK](https://github.com/rdonalson/FPFL-React-Java/tree/main/API-JDK).

| Area | Status |
| ---- | ------ |
| Response envelope, global exception handling, sanitizer | ✅ Done |
| Auth — login, guest, register, refresh, logout, change password, me | ✅ Done |
| User roles (admin) | ✅ Done |
| Client logs | ✅ Done |
| Item types — CRUD with id changes | ✅ Done |
| Time periods — CRUD with id changes | ✅ Done |
| Health checks (`db`, `diskSpace`, `ping`), `/info`, Swagger UI, landing page | ✅ Done |
| Docker image, AKS deployment, CI/CD with tests | ✅ Done |
| Items (credits / debits with recurrence) | ✅ Done |
| Initial amount (auto‑created at registration and first read) | ✅ Done |
| Ledger readout + recurrence expanders (`Fpfl.Display`) | ✅ Done |
| Cards & Loans — `/accounts`, purchases, `/display/accounts`, "Card/Loan Payment" ledger debits *(new in V3)* | ✅ Done |
| Debt Paydown — `/debts`, `/display/paydown`, ledger deductions *(new)* | ✅ Done |

---

## 🧰 Development Notes

- Keep domain logic inside the bounded context that owns it
- Controllers stay thin — they delegate to services and map through DTOs; errors are thrown as domain exceptions and handled globally, never caught in controllers
- Use `Fpfl.Common` sparingly — only for true cross‑cutting concerns
- Sanitize user‑supplied strings through the sanitizer (`[StrictText]` / `[LenientText]` / `INoSanitize`) rather than ad‑hoc cleanup
- Take the user id from the access token's claims, never from the request body
- Don't add EF Core migrations — the schema comes from API‑JDK. New tables go in a numbered, re‑runnable script in [`db/`](db), run against local and Azure databases before the code that uses them is deployed
- Never commit `appsettings.*.json` secrets, `.env.docker`, or a real signing key
- If Visual Studio breaks on domain exceptions while debugging, they are expected — the global handler turns them into 4xx responses (adjust Exception Settings or continue)

---

## 📄 License

This project is licensed under the [MIT License](../LICENSE).
