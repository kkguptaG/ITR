# 11 — Next-Generation Capital Gains Engine & UI Module

> Status legend: **[LIVE]** shipped & tested · **[WIP]** partially built · **[PLAN]** designed, not yet built.
>
> This document is the architecture + delivery plan for upgrading Capital Gains from a single guided
> form into a full **Capital Gains Filing Ecosystem**: ClearTax-simple for beginners, CA-grade for
> professionals, government-compliant in output, fintech-modern in UX, and intelligent throughout.
> It is deliberately phased — each phase is independently shippable, verified (tests + schema
> conformance), and builds on the dynamic engine already in `Domain/TaxEngine`.

---

## 0. Where we are today (foundation already shipped) [LIVE]

The current engine is **not** a throwaway — it is the computational core this module extends:

| Capability | Where | Status |
|---|---|---|
| Per-asset rate routing (111A / 112A / 112 / 115BBH / slab) | `CapitalGainsCalculator` | LIVE |
| s.70 intra-head set-off + s.74 carry-forward | `CapitalGainsCalculator` | LIVE |
| 112A grandfathering (31-Jan-2018 FMV, s.55(2)(ac)) | `CapitalGainsCalculator` + `GrandfatherFmvLookupService` | LIVE |
| **CII indexation (s.48)** — auto-computed | `CapitalGainRules.IndexedCostOf` | LIVE |
| **Holding-term auto-derivation** (per-asset thresholds) | `CapitalGainDerivation` | LIVE |
| **Gift/inheritance/will cost+holding step-in** (s.49(1)/2(42A)) | `CapitalGainDerivation` | LIVE |
| **Rural agri-land exemption** (s.2(14)) | `CapitalGainDerivation` | LIVE |
| 54 / 54B / 54EC / 54F reinvestment exemptions | `CapitalGainsCalculator.ComputeExemption` | LIVE |
| Schedule CG + Schedule 112A JSON generation | `ItrJsonGenerationService.Cg` | LIVE |
| ISIN master + 31-Jan-2018 FMV master (lookup) | `Modules/Reference` | LIVE |
| Asset classes: ListedEquity, EquityMF, DebtMF, UnlistedShares, ImmovableProperty, AgriculturalLand, Bonds, Gold, Jewellery, CryptoVda, Other | `CapitalGainAssetType` | LIVE |
| Dynamic, asset-driven capital-gains form | `income-forms.tsx → CapitalGainForm` | LIVE |

**The gap** this module closes: richer asset *sub-types* (ESOP/RSU/SGB/buyback/slump-sale/foreign),
bulk + AI ingestion, an insight/optimization/risk intelligence layer, and a fintech-grade
dashboard + guided/professional dual-mode UX.

---

## 1. LAYER 1 — Capital Asset Category Engine

### 1.1 Two-level taxonomy: `Category` → `SubType`

Today asset type is a flat enum. We introduce a **two-level model** so the UI can show 8 category
cards while the engine keeps precise, law-mapped behaviour per sub-type.

```
Category (UI card)              SubTypes (drive treatment)
─────────────────────────────  ────────────────────────────────────────────────────────────
EquityShares                   ListedEquity, UnlistedShares, IpoShares, EsopShares, RsuShares,
                               BonusShares, RightsShares, Buyback115QA
MutualFunds                    EquityMF, DebtMF (specified—always slab post-1Apr2023), HybridMF,
                               InternationalFund, GoldMF
RealEstate                     ResidentialHouse, CommercialProperty, Plot, AgriculturalLandRural,
                               AgriculturalLandUrban, InheritedProperty(modifier), JointOwnership(modifier)
GoldPrecious                   PhysicalGold, GoldEtf, SovereignGoldBond, Jewellery, OtherBullion
BondsSecurities                ListedBond, UnlistedBond, Debenture, GovtSecurity, TaxFreeBond, SovereignBond
VirtualDigitalAsset            Crypto, Nft  (s.115BBH: flat 30%, no set-off, no expense except cost)
ForeignAssets                  ForeignShare, UsStock, ForeignEtf, ForeignRsu, AdrGdr
OtherAssets                    Goodwill, IntangibleAsset, ArtCollectible, Vehicle(if business), IpRights, SlumpSale
```

**Mapping to tax behaviour** stays *law-as-data* (rule-set JSON): each sub-type maps to
`{ holdingThresholdMonths, indexationEligible, specialSection (111A/112A/112/115BBH/50/115QA),
exemptionsAllowed[], sttRelevant, foreign, defaultExempt }`. The engine reads this map; no
hard-coding of new assets in C# switch statements going forward (the switch becomes a fallback).

### 1.2 Modifiers (orthogonal to sub-type)

`acquisitionMode` (Purchase/Gift/Inheritance/Will) and `jointOwnershipPct` and `isRural` are
**modifiers** — already partly LIVE (`CapitalGainAcquisitionMode`, `IsRuralAgriculturalLand`). We add
`coOwnerPct` (gain apportioned), and `previousOwner*` (LIVE).

**Build:** extend `CapitalGainAssetType` → keep as `Category`; add `CapitalGainSubType` enum + a
`SubType` column; seed the sub-type→behaviour map in `capital_gains.asset_rules` of the rule-set.

---

## 2. LAYER 2 — Smart Data Collection

Three ingestion paths, all converging on the same `CapitalGainDraft` staging model before they
become validated `CapitalGain` rows.

### 2.1 Manual entry (guided wizard) [LIVE → enhance]
The dynamic `CapitalGainForm` already asks only the relevant questions per asset + auto-derives
term/indexation/section. Enhancements: STT-paid toggle (drives 111A/112A eligibility), TDS-on-sale
capture (194-IA property / 195 NRI), per-lot multiple acquisition dates.

### 2.2 Bulk import [PLAN — Phase 4]

```
Upload (CSV/XLSX/JSON)  ─►  Source profile detect  ─►  Column mapper  ─►  Normalizer  ─►
                            (Zerodha/Groww/Upstox/                         CapitalGainDraft[]
                             AngelOne/ICICIdirect/CAMS/KFintech/
                             Binance/CoinDCX/WazirX/Generic)
```

- **Source profiles**: a registry of `ImportProfile { id, displayName, detect(headers), columnMap,
  dateFormat, assetCategory, transform(row) }`. New brokers = new profile (data, not code).
- **Auto-mapping engine**: header fuzzy-match (`Levenshtein` + synonym dictionary: "Buy Date" ≈
  "Purchase Date" ≈ "acquisitionDate").
- **Error-correction assistant**: rows with unparseable/missing fields surface in a fixup grid.
- **Duplicate detection**: hash `(isin|scrip, buyDate, sellDate, qty, sellValue)`; flag collisions
  across imports.
- **CAMS/KFintech** (MF) and **broker P&L / tradebook** are the highest-value first profiles.

### 2.3 AI-assisted parsing [PLAN — Phase 5]
Reuse the existing document pipeline (`Modules/Documents`, `ExtractionService`, docs 05/10) — the
platform already does OCR + structured extraction for Form-16/26AS.

```
PDF/Image (contract note, broker CG statement, CAMS PDF, sale deed, demat holding)
   ─► OCR/text layer  ─► Claude structured-extraction prompt (schema-constrained)
   ─► CapitalGainDraft[] with per-field confidence  ─► human review grid  ─► commit
```
The extractor returns asset type, buy/sell dates, consideration, cost, STT/TDS flags, and a
confidence per field; low-confidence fields are highlighted for the user. **No silent auto-commit**
— extraction always lands in the review grid.

### 2.4 AIS/TIS import [WIP — partially possible today]
AIS already carries SFT-17/SFT-18 (securities/MF) + 26QB (property TDS). Map AIS CG lines to drafts;
**reconcile** against user-entered/broker-imported rows → feeds the Risk layer's "AIS mismatch".

---

## 3. LAYER 3 — Advanced Tax Computation Engine

Pipeline (extends the live engine; new stages marked):

```
CapitalGain rows
  └─► [LIVE] CapitalGainDerivation  (effective dates, term, indexed cost, step-in, rural-exempt)
  └─► [PLAN] Sub-type router        (ESOP perquisite-vs-CG split, SGB redemption-exempt vs secondary,
  │                                  buyback 115QA company-paid → exempt in hand, slump-sale 50B,
  │                                  depreciable-asset 50 deemed-STCG)
  └─► [LIVE] Per-line section + rate (111A/112A/112/115BBH/slab)
  └─► [LIVE] s.70 intra-head set-off
  └─► [PLAN] Inter-head + b/f (74) optimization  (already partly in main engine; add CG-specific
  │                                              optimizer: which losses to absorb first to minimise tax)
  └─► [LIVE] 112A ₹1.25L exemption
  └─► [LIVE] Reinvestment exemptions 54/54B/54EC/54F  (+ [PLAN] 54GB)
  └─► [LIVE] Special-rate buckets → main engine (surcharge cap 15% on 111A/112A already handled)
  └─► [PLAN] Exemption Advisor + Loss-harvesting simulator (read-only "what-if")
```

### 3.1 Holding-period classification [LIVE]
`CapitalGainDerivation` derives STCG/LTCG from effective dates + per-asset threshold (long = held
strictly > threshold). Sub-types refine the threshold (e.g. SGB, listed bonds = 12m).

### 3.2 Indexed cost [LIVE]
`IndexedCostOf` (CII table FY2001-02…FY2024-25 in rule-set). Post-Budget-2024 only land/building
acquired before 23-Jul-2024 keeps the 20%-with-indexation option (engine keeps the lower tax).
**[PLAN]** multiple acquisition lots → per-lot indexation then aggregate.

### 3.3 Grandfathering [LIVE] — 112A, 31-Jan-2018 FMV, with NSE high lookup.

### 3.4 Special rules
- 111A / 112A / 112 / 115BBH — **LIVE**
- 50 (depreciable deemed-STCG) — **LIVE** via the depreciation flow (synthetic STCG)
- 54 / 54B / 54EC / 54F — **LIVE**; 54GB — **[PLAN]**
- Buyback (115QA, listed — post-Oct-2024 taxable as deemed dividend in hand) — **[PLAN]**
- Slump sale (50B) — **[PLAN]**

### 3.5 Loss engine [LIVE core, PLAN optimizer]
STCL/LTCL set-off + 8-yr carry-forward + brought-forward absorption are LIVE. **[PLAN]** an optimizer
that orders absorption to minimise tax (e.g. shield 30% slab STCG before 12.5% LTCG) and explains why.

### 3.6 Exemption Recommendation Engine [PLAN — Phase 6]
Read-only advisory over the computed result:
- *Eligibility checker* per section (asset type + holding + reinvestment window).
- *Deadline tracker*: 54EC 6 months; 54/54F 1yr-before/2yr-after (3yr construct); CGAS parking date.
- *Suggested amount*: how much to reinvest to fully shield the gain.
- *Tax-saving estimate*: Δtax with vs without the exemption.
> "Investing ₹X in 54EC bonds before {date} saves ≈₹Y."

---

## 4. LAYER 4 — Professional-grade UI/UX

### 4.1 Screen flow
```
/returns/[id]/capital-gains                     ← new CG hub (replaces the single income-step section)
  ├─ Summary Dashboard (default landing)        [PLAN P2]
  ├─ Asset Category grid (8 cards)              [PLAN P2]
  │    └─ Category drawer → add/import/AI        [PLAN P2/4/5]
  ├─ Transaction grid (filter/search/inline-edit)[PLAN P3]
  ├─ Guided Tax Assistant (right rail)          [PLAN P3]
  └─ Mode toggle: Beginner ⇄ Professional       [PLAN P2]
```
The existing `/returns/[id]/workspace` computation dashboard is the read-only computed view; the new
CG hub is the *input + insight* surface that feeds it.

### 4.2 Component hierarchy [PLAN]
```
<CapitalGainsHub>
  <CgModeToggle/>                       beginner|pro (persists per user)
  <CgSummaryDashboard>                  KPIs + charts
    <KpiCard ×6/>  <GainLossDonut/>  <TaxImpactBar/>  <CarryForwardStrip/>
  <AssetCategoryGrid>
    <AssetCategoryCard ×8/>            count + net gain badge per category
      └─ opens <CategoryDrawer>
            <AddMethodTabs: Manual | Import | AI/>
            <CapitalGainForm/>          (LIVE, sub-type aware)
            <ImportWizard/>             (P4)  <AiParseReview/> (P5)
  <TransactionGrid>                     (P3) virtualized, filter/search/inline-edit
  <GuidedAssistantRail>                 (P3) term glossary, warnings, suggestions
</CapitalGainsHub>
```
All built from the existing UI primitives (`@/components/ui`: Card, Field, Select, Input, Alert,
ProgressRing, MoneyField) + Tailwind tokens (brand/money/payable/ink) — no new design system.

### 4.3 Required components
- **A. Summary Dashboard** — Total STCG / LTCG / taxable / exempt / losses available / carry-forward /
  Δtax. Donut (gain by asset class) + bar (tax impact old-vs-new). Wired to live `POST /tax/compute`
  `SpecialIncome` + `CapitalGainsResult`.
- **B. Smart Transaction Grid** — filter by asset/broker, search ISIN/scrip, inline edit, bulk fix,
  error highlight. Virtualized for HNIs with 1000s of rows.
- **C. Guided Tax Assistant** — right rail: plain-language term explainer, inconsistency warnings,
  correction suggestions, visual section explainer.
- **D. Dual mode** — Beginner (minimal jargon, one question at a time) vs Professional (full schedule
  access, raw computation, manual overrides of term/indexed-cost/section).

### 4.4 Responsive / mobile [PLAN]
Mobile: category grid → single-column; transaction grid → card list; assistant rail → bottom sheet;
sticky "tax impact" footer. Desktop: 3-pane (grid · table · assistant).

### 4.5 Visual language
Premium fintech: soft shadows (`shadow-card`), rounded-2xl cards, light gradients
(`bg-hero-gradient`), money=emerald / payable=amber / brand=indigo / ink=neutral. No government-form
density; tables are progressive-disclosure, not the primary surface.

---

## 5. LAYER 5 — ITR compliance + schedule generation

| Schedule | Status | Notes |
|---|---|---|
| Schedule CG (ScheduleCGFor23) | **LIVE** | per-property sales, rate buckets, set-off matrix |
| Schedule 112A (scrip-wise) | **LIVE** | grandfathering BE/AE split |
| Schedule SI (special income) | **LIVE** | 111A/112A/112/115BBH/115BB |
| Schedule CFL (carry-forward) | **LIVE** | STCL/LTCL 8-yr |
| Schedule AL (assets/liabilities) | **LIVE** (separate module) | link high-value assets |
| Schedule FA / FSI / TR (foreign) | **WIP** | foreign CG sub-types feed FA |
| ITR auto-recommend + invalid-combo detection | **LIVE** (`ItrSelectorService`) | extend for CG-only nuances |

ITR routing: CG ⇒ excludes ITR-1; ITR-2 (no business), ITR-3 (business/F&O), ITR-5/6 (firm/company).
`ItrSelectorService` already cascades; **[PLAN]** add CG-specific rules (e.g. unlisted shares ⇒ ITR-2+,
crypto-as-business ⇒ ITR-3).

---

## 6. Intelligent features [PLAN — Phase 6/7]

1. **Tax-risk alerts** — high-value property vs 50C stamp value; AIS mismatch (Δ vs imported AIS CG);
   missing PAN/TAN (buyer 194-IA); implausible holding period; notice-risk heuristics.
2. **Duplicate detection** — hash-based across imports + contract notes + repeated ISIN/qty/date.
3. **Optimization** — loss harvesting, exemption utilisation, "hold N more days for LTCG" alerts.
4. **Compliance heatmap** — per-row + overall confidence: 🟢 safe / 🟡 review / 🔴 likely mismatch,
   computed from validation + AIS reconciliation + completeness.

---

## 7. Database schema deltas

```
CapitalGain (extend existing)
  + Category            (enum, was AssetType)         [P1]
  + SubType             (enum)                          [P1]
  + SttPaid             (bool)                           [P1]
  + TdsOnSale           (decimal) + TdsSection           [P1]
  + CoOwnerPct          (decimal, default 100)           [P1]
  + SourceRef           (string: import batch / doc id)  [P4/5]
  + Lots                (1-n CapitalGainLot)             [P3] multiple acquisition lots
  (LIVE already: AcquisitionMode, PreviousOwner*, IsRuralAgriculturalLand, IndexedCost, FMV31Jan2018)

CapitalGainLot (new)            [P3]   per-lot buy date/qty/cost/improvement → per-lot indexation
CapitalGainImportBatch (new)    [P4]   source, fileRef, rowCount, status, mapping snapshot
CapitalGainDraft (new)          [P4/5] staging rows w/ per-field confidence + validation state
UserCgPrefs (new)               [P2]   mode (beginner|pro), default broker, column-map memory
```
All additive, Npgsql migrations, SQLite-test-safe (EnsureCreated). Enum widening is backward-compatible.

---

## 8. API surface

```
GET    /returns/{id}/capital-gains                  list (LIVE)
POST   /returns/{id}/capital-gains                  add  (LIVE; + sub-type fields)
PATCH  /returns/{id}/capital-gains/{gid}            edit (LIVE)
DELETE /returns/{id}/capital-gains/{gid}            (LIVE)
GET    /returns/{id}/capital-gains/summary          KPIs + per-category rollup + tax impact   [P2]
GET    /reference/cii?fy=2023                        CII value (law-as-data)                    [P2]
GET    /reference/asset-rules                        sub-type → behaviour map                   [P1]
POST   /returns/{id}/capital-gains/import            upload + map + dry-run → drafts            [P4]
POST   /returns/{id}/capital-gains/import/commit     commit reviewed drafts                     [P4]
POST   /returns/{id}/capital-gains/parse-document    AI extraction → drafts                     [P5]
GET    /returns/{id}/capital-gains/insights          risk alerts + optimization + heatmap       [P6]
GET    /returns/{id}/capital-gains/exemption-advice  eligibility + deadlines + savings          [P6]
```
All under the existing `Modules/Returns` + a new `Modules/CapitalGainsInsights` (Scrutor-scoped),
RFC7807 errors, FluentValidation, tenant-scoped, PAN/PII rules unchanged.

## 9. Validation rules (representative)
Sell ≥ 0; transfer ≥ acquisition (and ≥ previous-owner acquisition); STT-paid ⇒ listed equity/MF only;
54EC ≤ ₹50L & land/building only; 54B ⇒ agri land; VDA ⇒ no expense/exemption/set-off; coOwnerPct
0–100; import row must resolve asset category; AIS variance > threshold ⇒ warning (not block).

## 10. Error-handling flows
Import: per-row soft errors → fixup grid (never lose the batch). AI parse: low confidence → review,
never silent commit. Compute: a malformed rule-set degrades to defaults in the factory (LIVE) rather
than throwing before the engine. Network: optimistic UI with rollback (TanStack Query).

## 11. Beginner vs Professional
Beginner: category cards → one-question-at-a-time → auto everything → plain summary.
Professional: full grid, raw computation trace, manual overrides (term/indexed-cost/section/exemption),
bulk import, schedule preview. Mode persists in `UserCgPrefs`; the same data model underneath.

## 12. Extensibility principles
- **New asset = data** (sub-type → behaviour map in the rule-set), not a code change.
- **New broker = an `ImportProfile`** (registry entry), not a parser rewrite.
- **New AY = a new rule-set** (CII row, thresholds, rates) — engine unchanged.
- Engine stays **pure & deterministic** (docs 03 contract): same input + rule-set ⇒ byte-identical
  output, forever — essential for scrutiny years later.

---

## 13. Delivery roadmap (phased, each independently shippable + tested)

| Phase | Scope | Builds on |
|---|---|---|
| **P1** | Two-level taxonomy (Category+SubType), sub-type behaviour map, STT/TDS/coOwner fields, migration | LIVE engine |
| **P2** | CG Summary Dashboard + `/summary` API + asset-category grid + Beginner/Pro toggle | P1 |
| **P3** | Smart Transaction Grid + Guided Assistant rail + per-lot model | P2 |
| **P4** | Bulk import (CAMS/KFintech + broker P&L + generic CSV) + dedupe + fixup grid | P1 |
| **P5** | AI document parsing → review grid (reuse Documents pipeline) | P4 |
| **P6** | Insights: risk alerts, AIS reconciliation, exemption advisor, optimization, heatmap | P2/P4 |
| **P7** | Foreign-asset depth (FA/FSI/TR), buyback 115QA, slump-sale 50B, 54GB | P1 |

Phases are ordered by leverage: P1/P2 unlock the visible "next-gen" upgrade fastest; P4/P5 cut manual
entry; P6 is the intelligence differentiator. Every phase ships green tests + ITR schema conformance.
