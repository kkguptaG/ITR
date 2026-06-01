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
