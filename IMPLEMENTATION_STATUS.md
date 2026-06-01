# TallyG Tax Portal — Implementation Status

> **Last verified:** 2026-06-01 — integration/build-fix pass **plus** an independent re-verification
> (full build + 33/33 tests + live Sqlite smoke exercised the way the frontend calls the API), which
> caught and fixed **two further real defects** (Swagger 500 + string-enum binding). See
> [Post-build independent re-verification](#post-build-independent-re-verification-2026-06-01-opus-on-net-9-sdk).
> **Backend:** builds clean on .NET 9 (0 warnings, 0 errors), boots on the Sqlite no-infra path,
> and **all 33 tests pass**. Every module below was exercised at runtime against a live server
> (register → OTP → JWT → authorized reads/writes). See [What was fixed](#what-the-integration-pass-fixed).
> **Frontend:** static-checked only (this machine has Node 6; `npm`/`tsc`/`next build` were **not**
> run here). Imports against the foundation barrels, the `Role` union, and sidebar↔route wiring were
> verified by inspection and reconciled.

Legend: **Working** = real implementation, exercised end-to-end. **Working-with-stub** = real
implementation whose *external* dependency is a clearly-marked `// STUB:` returning realistic mock
data so the flow runs without third-party accounts. **Scaffolded** = compiles and is wired, but the
UI/logic is intentionally thin for the demo.

---

## Backend modules

| Module | Status | Notes |
|---|---|---|
| **Auth** (register, OTP request/verify, refresh rotation + reuse detection, logout, `me`) | **Working** | Matches the AUTH DTO contract verbatim. JWT (HS256) with `sub/tid/role/sid` claims; rotating opaque refresh tokens. **devOtp** returned only in `Development`. Verified full login flow + `/auth/me`. |
| **Tax engine** (`TallyG.Tax.Domain/TaxEngine`) | **Working** | Pure, AY-versioned, data-driven (all slabs/caps/rates from `TaxRuleSet.RulesJson`, nothing hardcoded). Line-by-line trace. Old-vs-new regime comparison, HRA, capital-gains buckets (111A/112A/112/115BBH), surcharge bands + marginal relief, 87A rebate, presumptive 44AD/ADA. Covered by 33 golden/contract tests. |
| **Tax API** (`/api/v1/tax`: compute, regime-compare, calculator, slabs, recommendations) | **Working** | `slabs` and `calculator` are `[AllowAnonymous]` (public calculator widget); compute/compare/recommend operate on the caller's own saved returns. `slabs` + `calculator` verified live. |
| **Returns / Filing** (`/api/v1/returns` CRUD + sub-resources: income-sources, salary, house-property, capital-gains, business-income, deductions; `:validate`, `:submit`, `:suggest-type`, status) | **Working-with-stub** | Full draft→compute→submit lifecycle. `:submit` e-files via the **ERI stub**. ITR auto-selector implemented. Verified create-return → add salary → add deduction → GET detail (status transitions Draft→InProgress). |
| **Documents** (`/api/v1/documents`: two-step pre-signed `:initiate-upload`→`:complete`, extraction, `extraction:approve`, download) | **Working-with-stub** | Two-step pre-signed upload (Decision D-2). Local-disk storage stub stands in for S3 pre-signed PUT/GET. **OCR extraction is a stub** (`ExtractionService`) returning realistic per-`DocumentKind` fields with confidences that straddle the 0.92 review gate to exercise the HITL path. |
| **Payments / Pricing** (`/api/v1/pricing/plans`, `/payments` orders + `:verify`, invoice, refund, webhooks) | **Working-with-stub** | Order/verify/signature via the **Razorpay stub**; idempotency-key replay; pricing math + GST. `pricing/plans` and `payments` list verified live (both were fixed this pass — see below). |
| **Wallet & Coupons** (`/api/v1/wallet`, `/wallet/transactions`, `:credit`; `/coupons:validate`, `:apply`) | **Working** | Wallet ledger + coupon validation/redemption. `wallet` and `wallet/transactions` verified live (transactions was fixed this pass). |
| **CA workflow** (`/api/v1/ca/queue`, `returns/{id}/assignment`, `review:approve`, `review:request-changes`, `ca/assignments/{id}`) | **Working** | Assignment + review lifecycle, role-gated to `CA|CaFirmAdmin|Reviewer`. RBAC enforced (verified a non-CA is correctly 403'd). |
| **Notices** (passive vault `/api/v1/notices`, responses, `:status`) | **Working** | Per Decision D-6, the **passive** notices vault ships (upload/view/manual status). Active auto-tracking is V2. List verified live. |
| **Support / Tickets** (`/api/v1/tickets`, messages, `:status`) | **Working** | Ticket threads + status. List verified live. |
| **Notifications** (`/api/v1/notifications`, `unread-count`, `:mark-read`) | **Working-with-stub** | In-app feed is real; outbound Email/SMS/WhatsApp delivery is the **console-logger stub**. List + unread-count verified live. |
| **Consent** (`/api/v1/consents`, `/me/consents`) | **Working** | DPDP-style consent grant/list/revoke. `me/consents` verified live. |
| **Admin — Users** (`/api/v1/admin/users`, `:status`, roles) | **Working** | Role-gated `Admin|SuperAdmin`. Verified live (was 403-blocked before the auth fix). |
| **Admin — Returns / Doc-queue** (`/api/v1/admin/returns`, `:assign-ca`, `doc-verification-queue`) | **Working** | Role-gated `Admin|Ops|SuperAdmin`. Both verified live. |
| **Admin — Analytics** (`/api/v1/admin/analytics/overview, revenue, filings`) | **Working** | In-memory bucketing over a bounded window so it runs identically on Sqlite + Postgres. All three verified live. |
| **Admin — CRM / Leads** (`/api/v1/admin/leads`, `pipeline`, `:stage`, activities) | **Working** | Pipeline + activity timeline. List + pipeline verified live. |
| **Admin — Audit** (`/api/v1/admin/audit`) | **Working** | Audit-log query + writer. Verified live. |
| **Reporting / DocGen** (`/api/v1/returns/{id}/acknowledgment, computation, documents`; `/payments/{id}/invoice:pdf`) | **Working-with-stub** | ITR-V, computation worksheet, and tax-invoice PDFs are generated by a **simple PDF generator stub** (`SimplePdfGenerator`) — real bytes, demo-grade layout. |
| **Persistence / Seeding** (`AppDbContext`, `DbInitializer`, `SeedRuleSet`) | **Working** | All ~45 DbSets; snake_case; JSON columns are `jsonb` on Postgres / `text` on Sqlite; soft-delete filters; idempotent seed (tenant, 8 roles + permissions, admin/demo users, AY2025-26 + rule-set + questionnaire, plans, coupon). |

### Stubbed external integrations (all marked `// STUB:`)
| Integration | Stub implementation | Behaviour |
|---|---|---|
| Payment gateway (Razorpay) | `Infrastructure/Services/RazorpayStub.cs` | Deterministic mock order id, payment id, and signature verify. |
| ITD ERI e-file | `Infrastructure/Services/MockEFilingClient.cs` | Returns a mock acknowledgment number + ITR-V on `:submit`. |
| OCR / document extraction | `Api/Modules/Documents/ExtractionService.cs` | Per-`DocumentKind` mock fields with seeded confidences (exercises the HITL review gate). |
| SMS / Email / WhatsApp | `Infrastructure/Services/ConsoleNotificationSender.cs`, `ConsoleOtpSender.cs` | Logs the message/OTP to the console (the OTP is also surfaced as `devOtp` in Development). |
| Object storage (S3 pre-signed) | `Infrastructure/Services/LocalDiskStorage.cs` | Local-disk file store + local upload/download endpoints stand in for S3 pre-signed PUT/GET. |
| PDF generation | `Infrastructure/Services/SimplePdfGenerator.cs` | Emits real, minimal PDF bytes for ITR-V / computation / invoice. |

Everything **not** in this table (auth, tax engine, persistence, all business logic and validation) is really implemented.

---

## Frontend modules (static check only — npm not run here)

| Area | Status | Notes |
|---|---|---|
| Foundation (`lib/api.ts`, `lib/auth.tsx`, `lib/format.ts`, `lib/api-types.ts`, `components/ui/*`, AppShell/Sidebar/Topbar, providers, i18n en/hi) | **Working** (by inspection) | Axios instance with token attach + single-flight refresh-on-401; in-memory access token + localStorage refresh; RFC 7807 → typed `ApiError`. UI barrel exports verified against every feature import. |
| Auth pages — `(auth)/login`, `/register`, `/verify-otp` | **Working** (by inspection) | OTP flow wired to the real auth DTOs; `devOtp` surfaced for the demo. |
| Filing wizard — `(app)/returns/[returnId]/file/[step]` + `features/filing/*` | **Working** (by inspection) | Multi-step guided wizard (personal → income → deductions → regime → documents → summary → payment → file) with react-hook-form + zod. |
| Returns — `(app)/returns`, `/returns/[returnId]` | **Working** (by inspection) | List + detail; status badges. |
| Documents — `(app)/documents` + `features/documents/*` | **Working** (by inspection) | Upload dropzone + extraction-review drawer against the two-step upload + HITL endpoints. |
| Payments — `(app)/payments` + `features/payments/*` | **Working** (by inspection) | Plan picker, checkout dialog, wallet panel, coupon field, history table. |
| Dashboard — `(app)/dashboard` + `features/dashboard/*` | **Working** (by inspection) | KPIs, recent returns, deadlines, refund tracker. |
| Support — `(app)/support` (Tickets · Notices · Notifications tabs) + detail routes | **Working** (by inspection) | Single hub with `?tab=` deep-linking; notifications bell. |
| CA review — `(app)/ca-review`, `/ca-review/[assignmentId]` + `features/ca/*` | **Working** (by inspection) | Queue table, return summary, review comment thread + action panel. |
| Admin — `(admin)/admin` (overview, users, returns, leads, analytics, audit) + `features/admin/*` | **Working** (by inspection) | Tables, stat cards, bar chart, assign-CA / lead / user modals, pagination. |
| Settings & Help — `(app)/settings`, `/help` + `features/settings/*` | **Working** (by inspection) | Profile, consents, language preference, security card. |
| Sidebar navigation (`components/layout/nav-config.ts`) | **Working** (fixed this pass) | Re-aligned to real routes — see below. |

> **Caveat:** the frontend has not been type-checked or built on this machine (Node 6 only). Run
> `npm install && npm run typecheck && npm run build` on a Node 18+ machine before relying on a
> production build. The static review found the foundation contracts and route wiring consistent.

---

## What the integration pass fixed

Three **real, boot-/request-breaking** backend defects were found by building, then actually
running the API and exercising every endpoint (a clean compile did **not** catch any of them):

1. **DI registration failure (app would not boot).**
   `MockExtractionService : IExtractionService` did not satisfy the Scrutor convention
   (`*Service` → `I*Service`, `AsMatchingInterface`), so `IExtractionService` was never registered
   and `DocumentService` could not be constructed → container validation threw at startup (exit 82).
   **Fix:** renamed the class/file to `ExtractionService` (Api/Modules/Documents). Now binds and boots.

2. **Sqlite ORDER BY on `decimal` / `DateTimeOffset` (500s on the no-infra demo path).**
   `pricing/plans` (orders by `decimal Price`), and `payments` + `wallet/transactions`
   (paginate by `DateTimeOffset CreatedAt`) threw `SqliteException: SQLite does not support
   expressions of type … in ORDER BY` at request time.
   **Fix (central, one place):** `AppDbContext.ConfigureSqliteValueConversions` now stores every
   `DateTimeOffset`/`DateTimeOffset?` as UTC ticks (`long`) **on Sqlite only** (Postgres keeps
   native `timestamptz`), which makes all such server-side ORDER BY/compare queries translate.
   `PaymentService.GetPlansAsync` was additionally switched to client-side ordering. All affected
   endpoints now return 200.

3. **JWT role authorization broken for every privileged endpoint (403 for valid Admin/CA/Ops).**
   `JwtBearerOptions.MapInboundClaims` defaulted to `true`, which rewrote the `role` claim to the
   long WS-* URI; that no longer matched `TokenValidationParameters.RoleClaimType = "role"`, so
   `[Authorize(Roles=…)]` rejected even correctly-issued tokens. Confirmed with a minimal
   reproduction (token carried `role=Admin`, yet `IsInRole("Admin")` was `false`).
   **Fix:** `options.MapInboundClaims = false` in `Program.cs` (keeps the compact `sub/tid/role/sid`
   claim contract intact on both minting and validation). All 9 admin endpoints + CA endpoints now
   authorize correctly, and a non-privileged `User` is still correctly 403'd.

**Frontend (static):** `components/layout/nav-config.ts` linked to **five non-existent routes**
(`/notices`, `/tickets`, `/admin/tenants`, `/admin/cas`, `/admin/coupons`) and **omitted two real
ones** (`/admin/leads`, `/admin/analytics`). Re-pointed Tickets/Notices/Notifications to the single
`/support` hub, removed the dead admin links, and added the existing Leads + Analytics links; added
`adminLeads`/`adminAnalytics` keys to `messages/en.json` + `hi.json` and removed the now-unused
`adminTenants/adminCas/adminCoupons` nav keys.

### Residual / out of scope
- Frontend not type-checked or built here (Node 6). Run `npm run typecheck && npm run build` on Node 18+.
- Latent (non-breaking): `IPasswordlessTokenService` is registered as a singleton in Infrastructure
  **and** matched by the Api Scrutor scan (scoped); the scoped registration wins at resolve time.
  It still resolves correctly and is only consumed by scoped services, so behaviour is unaffected —
  noted for tidiness, not fixed (would require touching foundation DI without functional benefit).

---

## Post-build independent re-verification (2026-06-01, Opus on .NET 9 SDK)

Re-ran the pipeline independently and drove the live API the way the **frontend** does (string enums,
two-step flows). A clean compile and the integration pass both missed **two more real defects**, now
fixed and re-verified (`dotnet build` 0 warn/0 err, `dotnet test` 33/33, API booted on Sqlite):

4. **Swagger generation returned 500 (broken API explorer).** Two modules each define a DTO named
   `ReturnSummaryDto` (`Modules.Returns` + `Modules.Ca`); Swashbuckle's default *short-name* schema IDs
   collided, so `GET /swagger/v1/swagger.json` threw `InvalidOperationException: schemaId already used`.
   **Fix:** `options.CustomSchemaIds(t => t.FullName!.Replace("+", "."))` in `Program.cs`. Swagger now
   serves **200** with all **94** endpoints.
5. **String enum bodies rejected → the whole filing flow 400'd (`REQUEST.MALFORMED`).** No
   `JsonStringEnumConverter` was registered, so any request body containing an enum (`itrType`,
   `regime`, …) failed to deserialize and bound to a null model — but the frontend sends string unions
   (`'ITR1'`, `'New'`). Every UI *create-return* and *compute* call would have failed (and responses
   returned integers the UI couldn't read). **Fix:** registered `JsonStringEnumConverter` in
   `AddControllers().AddJsonOptions(...)`. Verified: `POST /returns` → **201** with `"itrType":"ITR1"`;
   `POST /tax/calculator` → **200** emitting enum names.

**Live smoke results (independent):**
- `register` → `otp/request` (`devOtp` returned in Development) → `otp/verify` (471-char JWT) → `auth/me`
  returning the correct user + roles — all **200**.
- `POST /api/v1/returns` → **201** (enums round-trip as strings).
- Tax engine over HTTP, salaried ₹15,00,000, AY2025-26: **New ₹1,30,000 vs Old ₹2,57,400** (no extra
  deductions); with ₹1,50,000 80C: **Old ₹2,10,600**, New unchanged (80C correctly disallowed under the
  New regime) — matching hand calculation to the rupee, consistent with the 33 golden unit tests.
- Frontend: all JSON valid; i18n `en`/`hi` at **693 keys each, full parity**; `package.json` scripts
  `dev/build/start/lint/typecheck` present (build still pending a Node 18+ machine).

### Follow-up fix — Docker/Postgres path (would not create tables)

6. **`docker compose up` booted a schema-less Postgres.** `Program.cs` called `MigrateAsync()` on the
   Postgres branch, but **no EF migrations are bundled** — so Postgres would get only the
   `__EFMigrationsHistory` table (no domain tables) and the app/seed would fail. (Only the Sqlite
   no-infra path, which uses `EnsureCreated()`, actually worked.) **Fix:** the Postgres branch now
   uses `EnsureCreated()` when no migrations exist, and `Migrate()` once migrations are added —
   so the full schema (incl. Npgsql `jsonb` columns) is created on first boot. Re-confirmed the
   Sqlite path still boots cleanly after the change. *(Verifiable end-to-end only with Docker/Postgres,
   which isn't installed here; the logic is provider-correct and the Sqlite regression passed.)*

**Full persisted filing lifecycle re-verified end-to-end (the exact path the wizard drives):**
`register → create ITR-1 (201) → add ₹15,00,000 salary → add ₹1,50,000 80C → POST /tax/compute`
(persists `TaxComputation`, returns the old-vs-new comparison) → return status transitions
`Draft → ComputedReady`. Engine output: **Old ₹2,09,851** (GTI ₹14,47,600 after standard + professional-tax
deductions; taxable ₹12,97,600 after 80C) vs **New ₹1,30,000**; recommends New, saving **₹79,851** —
correct to the rupee and consistent with the 33 golden unit tests.

---

## Seeded credentials & dev-OTP behaviour

Passwordless OTP. Two seeded accounts in the default **retail** tenant (idempotent seed, every boot):

| Login (email **or** mobile) | Role | Use for |
|---|---|---|
| `admin@itrhelp.com` / `+919000000001` | Admin | Admin / back-office screens |
| `demo@itrhelp.com` / `+919000000002` | User | The taxpayer filing journey |

- **In `Development`** (both the Docker stack and the no-Docker path run as Development), the OTP code
  is returned in the `POST /api/v1/auth/otp/request` response as **`devOtp`** — log in with no SMS/email
  provider. The console-OTP stub also logs it.
- Login flow: `POST /auth/otp/request {identifier, purpose:"login"}` → `{otpToken, expiresInSeconds, devOtp}`,
  then `POST /auth/otp/verify {otpToken, code}` → `{accessToken, refreshToken, user}`.
- Seeded reference data: **AY2025-26** (filing open) + its rule-set (v1.0.0) + questionnaire schema;
  plans `free` / `plus` / `assisted`; coupon `WELCOME50`.
- Dev secrets (JWT signing key, OTP HMAC key, Razorpay keys) are placeholders in `appsettings.json`
  for the local demo only — override via env/config in any real environment.

---

## Run commands

### A — Docker (full stack: Postgres + Redis + API + Web)
```bash
docker compose -f infra/docker-compose.yml up --build
# Web http://localhost:3000 · API/Swagger http://localhost:5080/swagger · Health http://localhost:5080/health
docker compose -f infra/docker-compose.yml down -v   # stop + wipe
```
API runs on **Postgres** here (auto-migrated + seeded) in the Development environment.

### B — No Docker (embedded Sqlite; no DB server needed)
```bash
# Backend (Sqlite via appsettings.Development.json: Database:Provider = Sqlite)
cd backend
dotnet run --project src/TallyG.Tax.Api
# API http://localhost:5080 · Swagger http://localhost:5080/swagger
# Sqlite file: backend/.localstore/tallyg-tax.db  (delete to reset; recreated + reseeded on boot)

# Frontend (needs Node 18+ — NOT runnable on this machine's Node 6)
cd frontend
npm install
npm run dev          # http://localhost:3000  → API at http://localhost:5080/api/v1
```
> If you changed schema/storage and have an older Sqlite file, delete `backend/.localstore/tallyg-tax.db*`
> so `EnsureCreated()` rebuilds it (this pass changed how `DateTimeOffset` is stored on Sqlite).

### C — Tests
```bash
cd backend
dotnet test          # 33 passing: tax-engine golden vectors + foundation/enum contract tests
```

### Frontend static checks (run on a Node 18+ machine)
```bash
cd frontend
npm install
npm run typecheck    # tsc --noEmit
npm run build        # next build
```
