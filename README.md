# TallyG Tax Portal

A full-stack SaaS for **online Indian Income Tax Return (ITR) filing** — built for individuals,
salaried employees, pensioners, freelancers, professionals, traders, and MSMEs. It takes a
taxpayer through the whole journey: **OTP login → guided questionnaire → document upload &
extraction → automatic tax computation with old-vs-new regime comparison → pay → e-file →
ITR-V acknowledgment & status tracking.**

- **Backend** — ASP.NET Core 9 (C#), a pure assessment-year-versioned tax engine, PostgreSQL
  (or Sqlite for a zero-infra demo), JWT + OTP auth, modular feature areas.
- **Frontend** — Next.js 14 (App Router, TypeScript), a mobile-first guided wizard, TanStack
  Query, Tailwind, EN/HI i18n.
- **Infra** — one-command Docker Compose stack (Postgres + Redis + API + Web).

External integrations (Razorpay, the ITD ERI e-file API, OCR extraction, SMS/Email/WhatsApp)
are **stubbed with realistic mock data** so the entire end-to-end flow runs without any external
accounts. Everything else is really implemented.

---

## Architecture

The authoritative design lives in [`docs/architecture/00-overview.md`](docs/architecture/00-overview.md)
— start there. It is the map and the source of truth, and its **Decision Log (§6) is binding**.
Deep-dive chapters:

| # | Chapter | Covers |
|---|---------|--------|
| 00 | [Overview & Decision Log](docs/architecture/00-overview.md) | Canonical decisions, MVP scope, conflict reconciliation |
| 01 | [System Architecture & Tech Stack](docs/architecture/01-system-architecture.md) | Modular monolith, multi-tenancy, solution layout |
| 02 | [Database Schema & ERD](docs/architecture/02-database-schema.md) | ~45-table Postgres schema, indexing, JSONB, retention |
| 03 | [Tax Domain & Computation Engine](docs/architecture/03-tax-engine.md) | ITR auto-selector, AY-versioned engine, regime comparison |
| 04 | [API Design, Auth & RBAC](docs/architecture/04-api-and-auth.md) | REST catalog, OTP + JWT, 8-role RBAC, RFC 7807 errors |
| 05 | [AI, OCR & Document Processing](docs/architecture/05-ai-and-documents.md) | Vault, async OCR pipeline, HITL review |
| 06 | [Security, Compliance, DevOps & Scalability](docs/architecture/06-security-devops-scale.md) | Encryption, DPDP, AWS deploy, CI/CD, scaling |
| 07 | [Product Strategy, Roadmap & Business Model](docs/architecture/07-product-roadmap-business.md) | MVP→V2 roadmap, monetisation, KPIs |
| 08 | [Frontend, UX & Folder Structure](docs/architecture/08-frontend-ux.md) | Next.js wizard, screens, design system, i18n |
| 09 | [Document & Report Generation](docs/architecture/09-document-generation.md) | ITR-V / computation / invoice PDFs |
| 10 | [AI/OCR Persistence Layer](docs/architecture/10-ai-ocr-persistence.md) | OCR/RAG/risk/chat tables |

**Key binding decisions (Decision Log §6):** AWS + SQS (behind portability seams); two-step
pre-signed document upload; `:action` verb convention on sub-resources; v1 AIS is upload-based;
trader F&O routes to ITR-3.

### Repository layout

```
TallyGTax/
├─ backend/                       ASP.NET Core 9 solution (TallyG.Tax.sln)
│  ├─ src/TallyG.Tax.Domain/         entities, enums, the pure tax engine, service interfaces
│  ├─ src/TallyG.Tax.Infrastructure/ AppDbContext, EF Core, external-service stubs
│  ├─ src/TallyG.Tax.Api/            controllers, DTOs, auth, feature modules, Program.cs
│  ├─ tests/TallyG.Tax.Tests/        xUnit (tax-engine golden tests + contract tests)
│  └─ Dockerfile
├─ frontend/                      Next.js 14 App Router (TypeScript) + Dockerfile
├─ infra/                         docker-compose.yml, .env.example, init-db.sql
└─ docs/architecture/            the blueprint (read 00 first)
```

---

## Prerequisites

Pick a run path below; you only need the tools for the path you choose.

- **.NET 9 SDK** — for running/testing the backend without Docker.
- **Node.js 18+** (20 recommended) — for running the frontend without Docker.
- **Docker + Docker Compose** *(optional)* — for the one-command full stack.
- **PostgreSQL** *(optional)* — only if you want the backend on Postgres without Docker; the
  default no-Docker dev path uses an embedded Sqlite file and needs no database server.

---

## Run path A — one command with Docker (full stack)

Builds and starts Postgres, Redis, the API (on Postgres, schema auto-created + seeded), and the web app.

```bash
docker compose -f infra/docker-compose.yml up --build
```

Then open:

- **Web app:** http://localhost:3000
- **API + Swagger:** http://localhost:5080/swagger
- **Health probe:** http://localhost:5080/health

The API container runs in the `Development` environment so Swagger is enabled and the OTP code
is returned in the API response (see [Logging in](#logging-in)). To stop and wipe data:

```bash
docker compose -f infra/docker-compose.yml down -v
```

> Optional: copy `infra/.env.example` to `infra/.env` to override ports, credentials, or the API
> URL. Compose loads that file automatically. The defaults work out of the box.

---

## Run path B — local dev, no Docker

Two terminals.

**Backend** (uses the embedded **Sqlite** demo database — creates the schema and seeds itself on
first boot; no Postgres required):

```bash
cd backend
dotnet run --project src/TallyG.Tax.Api
```

- API: http://localhost:5080
- Swagger UI: http://localhost:5080/swagger

The Sqlite file is written to `backend/.localstore/tallyg-tax.db`. Delete it to start fresh.
(`appsettings.Development.json` pins `Database:Provider = Sqlite`; the production default is
Postgres.)

**Frontend:**

```bash
cd frontend
npm install
npm run dev
```

- Web app: http://localhost:3000 (proxies API calls to `http://localhost:5080/api/v1` by default)

---

## Run path C — tests

```bash
cd backend
dotnet test
```

Runs the xUnit suite, including the tax-engine golden vectors and the foundation contract tests.

---

## Logging in

Auth is **passwordless OTP**. The database is seeded with two ready-to-use accounts in the default
retail tenant:

| Login (email *or* mobile) | Role  | Use for |
|---------------------------|-------|---------|
| `admin@itrhelp.com` (`+919000000001`) | Admin | Admin / back-office screens |
| `demo@itrhelp.com`  (`+919000000002`) | User  | The taxpayer filing journey |

**In `Development`, the OTP code is returned directly in the API response as `devOtp`** — so you
can log in with no SMS/email provider configured. Both Docker (path A) and local dev (path B) run
in Development, so this works everywhere out of the box. The flow:

1. `POST /api/v1/auth/otp/request` with `{ "identifier": "demo@itrhelp.com", "purpose": "login" }`
   → returns `{ otpToken, expiresInSeconds, devOtp }`.
2. `POST /api/v1/auth/otp/verify` with `{ "otpToken": "...", "code": "<devOtp>" }`
   → returns `{ accessToken, refreshToken, user }`.

The web app does this for you — just enter a seeded email/mobile and the OTP shown.

> Seeded reference data also includes the active **AY2025-26** assessment year (filing open), its
> tax rule-set and questionnaire schema, the `free` / `plus` / `assisted` plans, and a
> `WELCOME50` coupon.

---

## Configuration notes

- **Database provider switch:** `Database:Provider` = `Postgres` (default) or `Sqlite`. The app
  builds the schema with `EnsureCreated()` on both providers (no EF migrations are bundled in this
  demo, so `docker compose up` works out of the box); if migrations are later added, Postgres
  applies them with `Migrate()` instead. Seeding is idempotent and runs on every boot.
- **Connection string:** read from `ConnectionStrings:Default`. In Docker it points at the `db`
  service; locally it defaults to a Sqlite file (Development) or `localhost:5432` (Postgres).
- **Secrets stay in config/env**, never in code. The committed dev keys (JWT signing, OTP HMAC,
  Razorpay) are placeholders for the local demo only — override them anywhere real.
- **Stubbed integrations** are marked `// STUB:` in the backend: Razorpay (mock order/verify),
  ITD ERI e-file (mock acknowledgment number + ITR-V), OCR extraction (mock parsed fields), and
  SMS/Email/WhatsApp (logged to the console).

---

## License

Proprietary — internal project. All rights reserved.
