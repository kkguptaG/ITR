# TallyG Tax — Commercial Launch & Income-tax Act, 2025 Compliance Plan

> ⚠️ **This is engineering/operational planning, not legal or tax advice.** Every tax-law specific
> (rates, slabs, section references, forms, due dates, computation rules) MUST be validated by a
> qualified **Chartered Accountant / tax counsel** against the **notified Income-tax Act, 2025**, the
> annual **Finance Act**, the **CBDT Rules**, and the **official ITR schemas/utilities** before it
> touches a real taxpayer's return. Do not present any computed figure to the public as authoritative
> until that validation is recorded.

## 1. The legal timeline that drives everything (verified against primary sources)

- The **Income-tax Act, 2025** came into force **1 April 2026**, repealing the 1961 Act, and applies
  from **Tax Year 2026-27** onward (income earned from 1 Apr 2026).
- It introduces a single **"Tax Year"** (Section 3), discontinuing *Previous Year* and *Assessment
  Year*; **536 sections** (vs 819); all **TDS consolidated under Section 393**. Tax *rates* are still
  set by the annual **Finance Act** (Finance Act 2026 amends the 2025 Act) — the Act is structural.
- **Transition (this is the crux):** income of **FY 2025-26 remains governed by the 1961 Act** and is
  assessed as **AY 2026-27**. CBDT notified the **AY 2026-27 ITR forms on 30 Mar 2026** (corrigendum
  10 Apr 2026).

| Income period | Governing Act | Filed as | When |
|---|---|---|---|
| FY 2024-25 & earlier | 1961 Act | AY 2025-26 etc. | done / belated |
| **FY 2025-26** | **1961 Act** | **AY 2026-27** | **live season now (due ~Jul/Sep 2026)** |
| **FY 2026-27** | **Income-tax Act, 2025** | **Tax Year 2026-27** | mid-2027 |

**Implication:** a product going live this season files under the **1961 Act (AY 2026-27)**. "Act 2025
compliance" is required for the **next** cycle (and for current-year advisory/projections). The engine
must run **both frameworks in parallel** through the transition — which the AY/version-keyed design
already anticipated.

Form changes already visible:
- **ITR-1 (AY 2026-27)** now permits **two house properties**.
- **ITR-2** drops the pre/post-23-Jul-2024 capital-gains bifurcation (single schedule).

## 2. The hard gates for a public, commercial platform (cannot be satisfied by code)

1. **ERI registration — the #1 gate.** To e-file for the public you must be a registered
   **e-Return Intermediary** with the ITD (via Protean/NSDL) **or** file through a partner ERI.
   Requirements include: eligible entity (company/CA/CS; **net worth > ₹1 crore** for a company),
   resident, no criminal record, **Class II/III Digital Signature Certificate**, a **due-diligence /
   security certificate from an ISA or CISA auditor**, ITD-approved systems, and official e-filing
   API onboarding.
2. **CA / tax-counsel sign-off** on the computation engine, ITR field mappings, and edge cases — per
   Act and per year, re-validated after every Finance Act.
3. **Security & data protection** — **DPDP Act 2025 + Rules** (consent, data-principal rights, breach
   reporting, India data residency), the ISA/CISA audit ERI requires, **VAPT**, and IT (Intermediary)
   Rules obligations (grievance officer, published T&C / privacy policy).
4. **Payments** — RBI-regulated PA/PG live onboarding (Razorpay/Cashfree), PCI-DSS scope owned by the
   gateway, correct **GST invoicing** on platform fees.
5. **Corporate/legal** — legal entity, professional-indemnity insurance, engagement T&C, refund
   policy, clear disclaimers, grievance redressal.

## 3. Codebase: where we stand vs production

**Strength:** the engine is **AY/version-keyed and data-driven** — supporting a new Act/year means
authoring a **new versioned rule-set + questionnaire + form schema**, not rewriting the engine (see
[Decision Log](architecture/00-overview.md)).

**Gaps to close for production:**
- Seeded rules are **AY 2025-26** — **one season stale**. Need **AY 2026-27 (1961 Act)** for the live
  season **and** a **TY 2026-27 (2025 Act)** track.
- Everything marked `// STUB:` must become a real, compliant integration: **ITD e-filing**, **payment
  gateway**, **OCR**, **SMS/Email/WhatsApp**, **S3 storage**, **PDF generation**.
- Production hardening: KMS encryption + secrets, DPDP consent/DSAR, VAPT remediation, observability,
  season-spike load/perf, backups/DR, **EF migrations** for Postgres, CI/CD quality gates.

## 4. Engineering plan toward readiness (every tax figure flagged `pending-CA-validation`)

- **E1 — Framework & data model:** add a first-class **TaxFramework/TaxYear** dimension
  (`Act1961` vs `Act2025`) beside the AY model; framework-aware terminology ("Tax Year" vs "AY/PY").
  Author **CA-reviewable** versioned rule-sets: **AY 2026-27 (1961 Act)** + a **TY 2026-27 (2025 Act)**
  scaffold — all slabs/limits/section refs as data, status `pending-CA`. Update form schemas (ITR-1
  two house properties; ITR-2 single CG schedule; §393 TDS for 2025-Act flows).
- **E2 — Correctness & assurance:** golden test packs **per framework/year**, reconciled to the
  **official ITD utility** outputs by a CA; a **rule-set lifecycle** (draft → CA-approved → active)
  with sign-off recorded in `audit_logs`.
- **E3 — Real integrations** behind the existing adapters: ITD e-filing (real ITR JSON + submission +
  ITR-V) via the chosen ERI route; AIS/26AS; PAN verification; live payments; OCR; notifications.
- **E4 — Production hardening:** security, DPDP, observability, load/DR, migrations, CI/CD gates.

## 5. Recommended sequence

1. **Now:** decide the ERI route + engage a CA; I build **E1** (framework model + AY 2026-27 scaffold)
   and begin **E4** hardening (these don't depend on legal sign-off).
2. **Pre-season:** CA-validated **AY 2026-27** rules + real e-filing (partner ERI) + live payments →
   controlled pilot with a small cohort.
3. **Post-season:** **TY 2026-27 (2025 Act)** track CA-validated and ready for the next cycle.

## Sources

- [ITD press release — IT Act 2025 in force from 01 Apr 2026](https://www.incometaxindia.gov.in/documents/d/guest/press-release-income-tax-act-2025-comes-into-force-from-01-april-2026-pdf)
- [PIB — IT Act 2025 to come into effect 1 April 2026](https://www.pib.gov.in/PressReleasePage.aspx?PRID=2221416&reg=3&lang=2)
- [ITD — CBDT FAQs on Interplay and Transition](https://www.incometaxindia.gov.in/documents/81799/11848482/FAQs-on-Interplay-and-Transition.pdf)
- [ITD — Objective and scope of the New Act](https://www.incometax.gov.in/iec/foportal/help/all-topics/e-filing-services/objective-and-scope-new-act)
- [New ITR forms AY 2026-27 — key changes](https://finlecture.in/indian-tax-system/new-itr-forms-ay-2026-27-key-changes/)
- [ClearTax — Income Tax Act 2025: key changes](https://cleartax.in/s/income-tax-act-2025)
- [ITD — e-Return Intermediary registration](https://www.incometax.gov.in/iec/foportal/help/eri/registration)
- [ClearTax — e-Return Intermediary (ERI): eligibility & registration](https://cleartax.in/s/e-return-intermediary-eri-income-tax)

## 6. Progress log

**2026-06-01 — E1 started: current-season framework + provisional rule-set lifecycle (build 0/0, 33/33 tests, live-smoke verified):**
- Rule-set model (`RuleSet.cs`) gained `act_framework` ("1961"/"2025"), `validation_status`
  ("pending-CA" → "ca-approved"), and a `disclaimer`. **Unmarked rule-sets default to provisional**
  (fail-safe for a public tax product).
- Seeded **AY 2026-27 (1961 Act, FY 2025-26)** as the **active** year
  (`SeedRuleSet.Ay2026_27Json`, `DbInitializer`) with the **Budget-2025 new-regime slabs**
  (₹0–4L nil … >₹24L 30%), ₹75k standard deduction, and the ₹12L / ₹60k 87A rebate. AY 2025-26
  retained as inactive/historical so prior-year returns still compute.
- Every computation (`/tax/compute`, `/tax/calculator`) now returns **`provisional`,
  `validationStatus`, `framework`, `disclaimer`** so the UI/users see figures are **not yet
  CA-authoritative**. Verified live: active AY = AY 2026-27, new-regime ₹15L → **₹97,500**,
  `provisional:true / pending-CA`.

**Immediate next steps (still open):**
- Frontend: render a **"Provisional — pending CA validation"** banner on the wizard Summary +
  computation views from the new flag (needs a Node 18+ build).
- Add **AY 2026-27 golden tests** to the test fixture; engage a CA to validate the figures and flip
  `validation_status` → `ca-approved`.
- ITR-1 **two house properties** for AY 2026-27 in the selector (`itr1_max_house_properties` is
  already in the rule-set, not yet wired into `ItrSelectorService`).
- **Partner-ERI e-filing adapter** (`IEFilingClient` → `PartnerEriEFilingClient`) against the chosen
  partner's API; replace the payment/OCR/notification stubs with live, compliant integrations.
- Author the **TY 2026-27 (Income-tax Act, 2025)** rule-set track for the next cycle.

**2026-06-01 (later) — Offline-filing ITR JSON + frontend build unlocked:**
- New **EReturn** backend module (pre-ERI offline-filing model): `POST /api/v1/returns/{id}/itr-json:generate`
  (ITD-format ITR-1 / ITR-4 JSON), `POST /api/v1/itr-json/{id}:validate` (rules-based pre-upload checks),
  `GET /api/v1/itr-json` (saved "ready to file" list), `GET /api/v1/itr-json/{id}:download`. One
  `ItrFiling` artifact per return; ITR-2/3 return a clear "mapper on the roadmap" 422. Verified live:
  generate → validate (correctly **blocks** on missing PAN) → list → download a structurally-correct
  ITR JSON. Backend build 0/0, 33/33 tests.
- **Honesty:** the JSON field mapping is modeled on the ITD schema and MUST be reconciled with the
  official downloadable **AY 2026-27** schema before real uploads; figures remain pending-CA. The user
  downloads the JSON and uploads it on the Income Tax portal after login (no ERI dependency).
- **Frontend now BUILDS for real** (first time ever): installed Node 20, `npm run typecheck` clean,
  `next build` succeeds — all **24 routes**. Fixed a query-keys self-reference, a missing type import,
  and a server-only `next/headers` leak into client components (split client-safe constants into
  `i18n-config.ts`).
- **Pending:** the Generate/Validate/Download **UI** (a filing/downloads page + File-step buttons),
  and running the real app end-to-end against the API.

**2026-06-01 (later still) — ITR-JSON UI wired + real app run end-to-end:**
- Frontend `ItrJsonPanel` (Generate / Validate / Download + the validation report) wired into the
  wizard **File step**; `features/filing/itr-json.ts` data layer. `npm run typecheck` clean and
  `next build` succeeds (File-step route bundles it).
- Ran the **real app end-to-end** (production Next.js on :3000 + the API on :5080): authenticated
  dashboard; the filing wizard Personal step with the ITR auto-recommendation; the Summary step
  showing the live AY 2026-27 computation (salary ₹11,25,000 → **s.87A rebate → ₹0 tax** under the
  Budget-2025 new regime, with statutory citations); and the File-step **ITR-JSON panel**
  generating/validating/downloading — reaching it by paying a return through the **real Razorpay-stub
  order→verify (HMAC-signature) flow**. The panel correctly surfaced the blocking `PERSONAL.PAN_MISSING`
  error and the `SCHEMA.RECONCILE` / `TAX.PROVISIONAL` warnings.
- Fixed a real **dev-only auth bug**: the `AuthProvider` bootstrap's `active` cleanup flag suppressed
  the only `/auth/me` result under React 18 StrictMode → `isLoading` stuck on "Loading…". Now
  StrictMode-safe (the `bootstrapped` ref already de-dupes). Production was unaffected.
- Run/screenshot note: use `next start` (via `frontend/_start.js`) for headless screenshots — dev's
  HMR websocket keeps the page from reaching network-idle so the screenshotter times out.

**2026-06-01 — Validation: per-issue resolution suggestions + table view:**
- `ValidationIssueDto` gained a **`Suggestion`** field; the validator now attaches a concrete
  "how to fix" to **every** error and warning (e.g., PAN_MISSING → "Add your PAN in Settings →
  Profile"; SCHEMA.RECONCILE → "Download the official AY 2026-27 schema and validate in the offline
  utility"; TAX.PROVISIONAL → "Have a CA validate the computation, then mark the rule-set approved").
- The `ItrJsonPanel` renders the report as a **table — Severity · Check · Issue · Suggested fix** —
  instead of plain lists. Backend build 0/0; frontend `next build` clean. Verified live against the
  running app (1 error + 4 warnings, each with a suggestion).

**2026-06-01 — Filings follow-ups (#1 report-on-load · #2 My Filings page · #3 ITR-2/3 mappers):**
- **#1 Report on load:** new `GET /api/v1/itr-json/{id}/report` returns the *stored* validation report;
  the File-step panel fetches it on open, so the issues+suggestions table shows immediately (no
  Re-validate click needed). Verified live: 5 rows render on load.
- **#2 My Filings page:** new `/filings` route (sidebar "Filings" entry, EN/HI label) lists every
  generated ITR JSON across the user's returns — file, AY, status, error/warning counts, Download,
  Open — via `GET /api/v1/itr-json`. Verified live (1 row, correct counts).
- **#3 ITR-2 / ITR-3 mappers:** `ItrJsonGenerationService` now emits **ITR-2** (ScheduleS/HP/**CG**/OS/VIA
  + PartB-TI/TTI/TaxPaid/Refund/Verification) and **ITR-3** (the same **plus ScheduleBP** for
  business/profession incl. F&O). Verified live: both produce the correct form envelope + schedules.
  Schedules remain provisional / reconcile-with-official, same as ITR-1/4.
- Backend build 0/0; frontend `next build` clean (`/filings` route bundled).

**2026-06-01 — Deploy prep + private-preview lock-down:**
- **Hosting decision:** Hostinger shared hosting (hPanel) cannot run ASP.NET Core 9 + Next.js SSR +
  PostgreSQL, so the app deploys to **Render** (managed: Postgres + two Docker web services) while
  Hostinger keeps the **domain + DNS** for `itrhelp.com`. Runbook: `infra/DEPLOY-RENDER.md`;
  blueprint: `render.yaml`. (Production DPDP residency → move to an India region later.)
- **Managed-Postgres fix:** `NormalizePostgresConnectionString` converts Render's
  `postgresql://user:pass@host/db` URI to Npgsql keyword format (SslMode.Prefer + TrustServerCertificate),
  since Npgsql doesn't parse the URI form natively.
- **Lock-down (so a public staging URL is not freely self-registerable), verified live in Production mode:**
  - `Auth__AllowSelfRegistration=false` → `RegisterAsync`, the signup-OTP path, and the
    create-on-verify path all refuse with **403 `AUTH.REGISTRATION_CLOSED`**. Only the seeded
    `admin@itrhelp.com` / `demo@itrhelp.com` exist.
  - `ASPNETCORE_ENVIRONMENT=Production` → `devOtp` is **null** in API responses; the login OTP is
    only written to the server log (`[OTP STUB] ... code=######`), which the operator reads from
    Render's **Logs** tab.
  - Smoke (Production + SQLite, the flag set): register → 403, signup-OTP → 403, login-OTP for the
    seeded demo → 200 with `devOtp:null` and the code present in the log. Backend build 0/0.
  - `render.yaml` corrected to the keys the code actually reads: `Auth__Jwt__SigningKey`
    (Render `generateValue`), `Auth__Jwt__Issuer`, `Auth__Jwt__Audience` (the earlier `Jwt__Secret`
    keys were dead config).
- **Source control:** the project was pushed to GitHub `kkguptaG/ITR` (main). NOTE: the assistant's
  own `git push` / `remote set-url` were blocked by the safety classifier (unverified external
  target); the user runs all pushes themselves. **Keep the repo Private** until the compliance gates
  are done. Re-enabling sign-up (`Auth__AllowSelfRegistration=true`) and wiring a real SMS/email OTP
  provider are launch-gated, not preview tasks.

**2026-06-01 — Depth build-out #1: Schedule S salary breakup + s.234A/B/C interest (vs a mature CA suite):**
- Context: benchmarked our computation/capture depth against a mature CA desktop product (CompuTax).
  Honest gap audit recorded; prioritised the two highest-frequency gaps first.
- **Salary breakup (Schedule S):** new `SalaryComponent` child of `SalaryDetail` + pure `SalaryRollup`
  (components → flat Gross/Perquisites/HRA-exempt/ExemptAllowances the engine already consumes, so the
  computation + ITR-JSON are unchanged). Frontend salary form gained a CompuTax-style grid
  (Particular · Type 17(1)/17(2)/17(3)/s.10 · Total · Exempt) with a live rollup summary. **Fixed a
  real bug:** profits-in-lieu (17(3)) was dropped before tax — now folded into the taxable base in
  both engine-input mappers + Schedule S. Live-verified: 4-component breakup → Gross ₹8.4L / Perq
  ₹1.2L / PIL ₹1L / HRA-exempt ₹1.44L; old-regime tax ₹89,128, new ₹0.
- **Interest u/s 234A/B/C:** pure `InterestCalculator` (rate + s.208 threshold data-driven from the
  rule-set, defaulted 1% / ₹10,000). 234A late-filing, 234B advance-tax default (≥90% test), 234C
  deferment (installment-wise, with safe-harbour; exact when payment dates are supplied, else the
  honest "no advance tax paid on time" assumption with a trace note). Wired into the engine
  (InterestPenalty + refund/payable now reflect it) with dates threaded from the AY via TaxService.
  Live-verified (as-of today): old regime ₹12L → 234B ₹3,276 + 234C ₹8,272; new regime ₹0 → no interest.
- Tests 39/39 green (added rollup + 234A/B/C). Still provisional / pending-CA. **Known next gaps**
  (from the audit): AMT 115JC/AMTC, relief 89/90/91, loss carry-forward, agri-income head, lottery
  115BB, full salary-component regime gating, and quarterly advance-tax capture for exact 234C.
- Build note: lingering `dotnet run` hosts held `Domain.dll` and caused a stale incremental build
  (engine ran old code) — kill stray `dotnet` + `dotnet build -t:Rebuild` before verifying.

**2026-06-01 — Depth build-out #2: Income-from-Other-Sources segregation (115BB + agri):**
- The audit flagged Other Sources as a "black box". Added a `nature` tag (normal / interest / dividend /
  lottery_115bb / agricultural) carried in `IncomeSource.SourceMetaJson` + the ad-hoc calculator DTO,
  threaded to the engine in both TaxService and ReturnService (via `ExtractNature`).
- **Winnings / casual income (s.115BB):** flat 30% (rule-set `casual_income_115bb_rate`), no deductions,
  no s.87A against it; counted in GTI + total income for surcharge/87A gating but taxed separately.
- **Agricultural income:** exempt (not in GTI) but partial-integration aggregated for rate —
  slabTax(normal+agri) − slabTax(agri+basic-exemption); the offset surfaces as the
  "Rebate on agricultural income" trace line (matches the CompuTax computation sheet). Threshold ₹5,000;
  basic-exemption derived from the regime's first 0% slab. Applies in both regimes.
- Frontend: Other-income form gained a **Nature** dropdown (stores `{"nature":…}` in SourceMetaJson).
- Tests 44/44. Live-verified end-to-end: salary 10L + lottery 2L + agri 5L → Casual115BB tax ₹60,000,
  agri rebate ₹62,500, totalTax ₹2,54,800, composing correctly with 234B/234C interest. Provisional / pending-CA.

**2026-06-01 — Depth build-out #3: brought-forward loss set-off (HP + business):**
- Engine sets off brought-forward (earlier-year) **house-property** and **non-speculative business**
  losses against the SAME head's current-year income only; the unutilised part is reported as still
  carried forward (trace: `*.BfLossSetOff` / `*.BfLossCarryForward`). Losses never cross heads.
- Inputs on `TaxComputationInput` + the ad-hoc `TaxCalculatorRequest`. Tests 47/47 (HP set-off,
  business set-off + carry-forward remainder, cross-head isolation). Live-verified via /tax/calculator:
  ₹3L business profit + ₹5L b/f loss → GTI ₹0, ₹2L carried forward.
- **Follow-ups (noted):** returns-level capture + UI for b/f losses; capital-loss set-off (STCL/LTCL vs
  the special-rate buckets); speculative-loss separation. Still provisional / pending-CA.

**2026-06-01 — Depth build-out #4: return-level prepaid-tax + b/f-loss capture (correctness fix):**
- **Bug fixed:** `BuildInputFromReturnAsync` hard-coded TDS/TCS/advance/SA tax to **₹0**, so every saved
  return overstated tax payable AND 234 interest (computed on the full tax, ignoring TDS already deducted).
- Added `TdsPaid / TcsPaid / AdvanceTaxPaid / SelfAssessmentTaxPaid` + `BroughtForwardHousePropertyLoss /
  BusinessLoss` to `TaxReturn` + `UpdateReturnRequest` (PATCH) + `ReturnDetailDto`; both engine-input
  builders (TaxService + ReturnService) now read them. (New columns → dev Sqlite recreated; Postgres prod
  needs EF migrations — next.)
- Live-verified: 12L salary + ₹1.5L TDS → tax ₹1,63,800, TDS credited, 234 interest ₹973 (was ~₹11.5k),
  net payable −₹14,773 (was −₹1,63,800+). b/f business loss stored + carries forward when no business income.
- **Follow-up:** frontend "Taxes paid & brought-forward losses" capture card (API-complete; UI next).
- Infra note: a stale **`TallyG.Tax.Api.exe`** (not `dotnet.exe`) held port 5080 + `Domain.dll` — kill
  that process name too, not just `dotnet`, before rebuild/restart.

**2026-06-01 — Depth build-out #5: capital-loss set-off + Summary capture card:**
- **Capture card:** the Summary step now has a "Taxes already paid & brought-forward losses" card
  (TDS/TCS/advance/SA + b/f HP/business loss) that PATCHes the return and recomputes — making the #4
  capture usable in the UI, not just the API.
- **Capital-loss set-off:** brought-forward STCL (vs slab-STCG → 111A → 112 → 112A) and LTCL (vs 112 →
  112A only) set off against the rate-specific CG buckets in a documented tax-minimising order; VDA
  (115BBH) never reduced; unused carries forward (trace `CG.Bf{Stcl,Ltcl}{SetOff,CarryForward}`).
  Inputs on `TaxComputationInput` + ad-hoc `TaxCalculatorRequest`. Tests 50/50 (STCL→111A ₹23,400,
  LTCL→112 ₹39,000, LTCL-can't-touch-STCG ₹31,200 + c/f). Set-off order pending CA validation.
- **Follow-ups:** returns-level capture + card fields for the two CG losses (mechanical); EF Core
  migrations for Postgres schema evolution (next). Still provisional / pending-CA.

**2026-06-01 — Production hardening #1: EF Core migrations (Postgres schema evolution):**
- **Why:** the Postgres prod path relied on `EnsureCreated`, which never ALTERs an existing DB — so any
  new table/column (salary_components, the tax_returns capture columns) would silently fail to deploy
  (the exact bug hit twice locally). Migrations fix this; Program.cs already prefers `Migrate()` when
  migrations exist.
- Added `dotnet-ef` as a **local tool** (`.config/dotnet-tools.json`, v9.0.6); referenced
  `Microsoft.EntityFrameworkCore.Design` in the Api (startup) project + added `compile` asset in
  Infrastructure; new `AppDbContextDesignTimeFactory` pins **Npgsql + snake_case** so migrations target
  the production schema (dev Sqlite is untouched, still `EnsureCreated`).
- Generated `Persistence/Migrations/InitialCreate` — full current model, Postgres `jsonb` + snake_case,
  incl. `salary_components` + `tax_returns.{tds_paid,advance_tax_paid,self_assessment_tax_paid,
  brought_forward_house_property_loss,brought_forward_business_loss}`. `dotnet ef migrations list` shows it;
  solution builds 0/0; 50/50 tests green.
- **Deploy caveat:** on Render Postgres, `Migrate()` applies it to a FRESH DB cleanly. If a Postgres DB
  was already created earlier via `EnsureCreated` (untracked by `__EFMigrationsHistory`), drop it (or
  baseline) before the first migrated deploy. New schema changes from now on = `dotnet ef migrations add`.
- Not applied to a live Postgres here (no local Postgres) — generation + compile + model-snapshot verified;
  apply happens on deploy.

**2026-06-01 — Code review of the session's changes (7 finder angles, high effort) + fixes:**
- **CRITICAL regression fixed:** the flat-salary entry path always sends `components: []`, and
  `ApplySalaryBreakup` treated a non-null (even empty) list as "rebuild from breakup" → rolled up an
  empty set → zeroed Gross/HRA/perquisites. Every plain-entry salary return would have been zeroed.
  Fix: treat null OR empty components as "keep the flat fields". Live-verified (gross ₹12L preserved).
- **Correctness fixed:** s.234A base wrongly subtracted self-assessment tax (which is paid at/after the
  due date — exactly what 234A penalises). Now base = tax − TDS/TCS − advance only. Regression test added.
- **Cleanup:** removed a duplicate unused `UpdateReturnBody` type (frontend returns/types.ts). Build/typecheck confirm it was unused.
- Tests 51/51; backend + frontend build clean.
- **Review findings queued (next):** (1) b/f CAPITAL losses (STCL/LTCL) are reachable only via the ad-hoc
  calculator — add them to TaxReturn + the capture card; (2) `ReturnService.BuildComputationInput`
  (filing-snapshot path) doesn't thread the 234 interest dates / CG losses, so the snapshot can disagree
  with `/tax/compute` — centralise the TaxReturn→input mapping; (3) dedupe `ExtractNature` (copied in two
  services). **Documented limitations (need more capture / CA sign-off):** 234B should stop interest at the
  self-assessment payment date (we capture amount, not date); 234C proviso excluding late-year CG/115BB
  income; 87A interaction with agri partial-integration; salary-component row ordering (CreatedAt ties).

**2026-06-01 — Review follow-ups closed (capital-loss capture + path consistency + dedup):**
- **Capital losses on real returns:** added `BroughtForwardShortTermCapitalLoss` /
  `BroughtForwardLongTermCapitalLoss` to `TaxReturn` + `UpdateReturnRequest` + `ReturnDetailDto` + the
  Summary capture card. New EF migration `AddCapitalLossCarryForward` (Npgsql). Live-verified on a return:
  LTCG-112 ₹4L + b/f LTCL ₹1L → taxed on ₹3L, GTI ₹3L, tax ₹39,000.
- **Path consistency:** extracted `TaxComputationInputFactory.FromReturn(...)` as the SINGLE
  `TaxReturn → TaxComputationInput` mapper, used by BOTH TaxService (/tax/compute) and ReturnService
  (filing snapshot) — they no longer diverge on prepaid taxes, b/f losses, 234 dates or Other-Sources
  nature; a new input field is edited in one place. (Snapshot age still defaults to an adult slab — documented.)
- **Dedup:** `ExtractNature` now lives once in the factory (was copied in two services).
- Tests 51/51; backend + frontend build clean; live compute on a real return reflects all of the above.

**2026-06-01 — Production hardening #2: OTP request throttle (anti-flooding):**
- `RequestOtpAsync` now rate-limits code requests **per identifier** (default **5 per 15-min window**,
  data-driven via `Auth:OtpMaxRequestsPerWindow` / `Auth:OtpRequestWindowSeconds`) → **429
  `AUTH.OTP_RATE_LIMITED`**. Complements the existing per-OTP attempt cap (5) + expiry + single-use +
  HMAC-hashed codes in `VerifyOtpAsync`. Closes the code's own "rate-limit deferred to gateway" TODO.
- Live-verified: 6 rapid requests for one identifier → five 200s then 429. Tests 51/51; build clean.
- Note: per-identifier (stops targeted SMS-bombing / cost abuse). A global per-IP limiter
  (ASP.NET Core RateLimiter) at the edge is still worth adding for distributed abuse before public launch.
