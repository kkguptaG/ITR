# CA Review & Sign‑off Pack — TallyG Tax computation engine

> Purpose: give a practising Chartered Accountant everything needed to **verify and certify** that the
> tax‑computation engine produces correct figures per the Income‑tax Act, 1961 and the applicable
> Finance Act, for the implemented scope. The figures the product shows users are **provisional /
> "software‑assisted, verify before filing"** until this sign‑off is in place.

Pinned for this review:
- **Engine commit:** `5f74f43` (deterministic — same input + same rule‑set ⇒ identical output, forever).
- **Rule‑sets:** AY2025‑26 `v1.0.0`, AY2026‑27 (Budget‑2025 new regime, 87A to ₹12L) — the seed lives in
  `backend/src/TallyG.Tax.Infrastructure/Persistence/SeedRuleSet.cs`.
- Sign‑off is **version‑pinned**: any rule‑set or engine‑logic change after sign‑off needs a delta re‑review.

---

## 1. Why review is tractable here
The engine is **pure, deterministic, and AY‑versioned**, and **nothing is hardcoded** — every slab, rate,
cap, threshold and date lives in the rule‑set JSON ("law as data"). Every output figure carries a
**line‑by‑line trace** with its section reference. So the review reduces to three checkable things:
1. **The numbers** — verify the rule‑set JSON against the Finance Act (§4).
2. **The formulas** — verify the per‑provision methodology (§5).
3. **The behaviour** — verify the worked examples / golden tests (§6), then run your own cases.

---

## 2. What we need FROM the CA (the deliverable)
1. A signed statement (template in §8) certifying the computation logic + the AY rule‑set values for the
   stated scope, **at the pinned engine commit + rule‑set versions**.
2. A ruling on each **open item** in §7 (accept the simplification, or specify the correction).
3. Any corrections as a marked‑up list (provision → what's wrong → correct treatment + section).
4. Agreement on the **re‑validation cadence**: re‑review on every Finance Act and on any rule‑set change.
5. (If the CA will also be the **ERI principal / tax advisor on retainer**) — separate engagement; noted but
   out of scope for *this* computation sign‑off.

The ideal reviewer: CA in practice, direct‑tax specialisation, comfortable reading a rate table + a
worked computation. ~1–2 days of effort given this pack.

---

## 3. Scope being certified
**Forms / persons:** ITR‑1/2/3/4; resident Individual / HUF; **old and new regime** (auto‑compared).

**Heads of income**
- **Salary** — 17(1) + perquisites 17(2) + profits‑in‑lieu 17(3); exempt allowances 10; **HRA 10(13A)**
  (old only); standard deduction 16(ia); professional tax 16(iii) (old only); Schedule‑S breakup roll‑up.
- **House property** — self‑occupied 24(b) interest (old only); let‑out NAV − 30% − interest; **loss set‑off
  capped ₹2,00,000**.
- **Capital gains** — STCG 111A; LTCG 112A (with **31‑Jan‑2018 grandfathering** + ₹1.25L exemption); LTCG 112
  (property: **lower of 20%‑with‑indexation vs 12.5%‑without** for pre‑23‑Jul‑2024 acquisitions); VDA 115BBH
  (flat 30%, no set‑off); **reinvestment exemptions 54 / 54F / 54EC** (54EC capped ₹50L).
- **Business/profession** — presumptive **44AD** (6%/8%) and **44ADA** (50%); normal net‑profit.
- **Other sources** — normal (slab); **casual/winnings 115BB** (flat 30%); **agricultural** (exempt, partial
  integration for rate).

**Deductions (Chapter VI‑A)** — 80C, 80CCD(1B), 80CCD(2) (% of salary), 80D (self/parents/preventive, age),
80TTA, 80TTB, **80U** (fixed 75k/1.25L severe), **80DD** (fixed 75k/1.25L), **80DDB** (40k/1L senior),
**80EEA / 80EEB** (1.5L), **80GG** (least‑of), **80G** (100%/50% × qualifying‑limit), 80E + profit‑linked
(80‑IA…, full); all **regime‑gated** (disallowed under 115BAC except 80CCD(2)/80CCH/80JJAA).

**Tax determination** — slab tax (age slabs); **87A** (both regimes, + new‑regime marginal relief);
**surcharge** (banded, marginal relief, 15% cap on special‑income tax); 4% cess; **AMT 115JC + credit 115JD**;
**interest 234A/B/C**; **reliefs 89(1) / 90 / 90A / 91**; **set‑offs** (b/f house‑property, business, STCL,
LTCL; current‑year capital‑gain bucket set‑off); rounding **288A/288B**.

**Explicitly NOT implemented** (please confirm OK to exclude / note for roadmap): company MAT 115JB;
clubbing & minor‑income; inter‑head set‑off of **current‑year business loss** (currently floored, not carried);
89(1) multi‑year auto‑computation (we consume the Form‑10E relief figure); per‑section CG asset‑class gating;
foreign‑asset (Schedule FA / FSI / TR) schedules; trust/AOP‑specific rules.

---

## 4. The numbers to verify (rule‑set JSON)
One file holds every figure — verify each against the Finance Act for the AY:
`SeedRuleSet.cs` → the AY JSON blocks. Key keys: `regimes.{old,new}.slabs` (+ senior/super‑senior),
`std_deduction_salary`, `rebate_87a {income_threshold, max_rebate, marginal_relief}`, `surcharge_bands`,
`surcharge_cap_special_income`, `cess`, `deduction_caps.*` (80C/80D/80U/80DD/80DDB/80EEA/80EEB/80GG/…),
`capital_gains.* {ltcg_112a_exemption, *_rate, grandfather_date_112a, property_indexation_cutoff,
section_54ec_cap}`, `presumptive.{44AD,44ADA}`, `hra.*`, `interest_monthly_rate`, `advance_tax_threshold`,
`casual_income_115bb_rate`, `agri_integration_threshold`, `amt_rate`, `amt_threshold_individual`,
`amt_addback_sections`.

---

## 5. Methodology (formula + section) — summary
| Provision | Engine treatment | Section |
|---|---|---|
| Slab tax | Banded marginal rates by age‑appropriate schedule | Finance Act Sch‑I / 115BAC |
| 87A rebate | Gated on total income ≤ threshold; against slab tax on normal income; new‑regime marginal relief above threshold | 87A |
| Surcharge | Highest applicable band on post‑rebate tax; 15% cap on tax attributable to 111A/112A/112; marginal relief at band edge | Finance Act |
| Cess | 4% on (tax after rebate + surcharge) | — |
| HRA exempt | least of (actual HRA, rent − 10% salary, 50/40% salary) | 10(13A) |
| CG 112A | gross − ₹1.25L exemption on net 112A; grandfathered cost = max(actual, min(FMV 31‑Jan‑18, sale)) | 112A |
| CG 112 property | lower **tax** of 20%‑indexed vs 12.5%‑unindexed (pre‑cutoff) | 112 / 112(1) proviso |
| CG 54/54F/54EC | 54/54EC exempt reinvested ≤ gain (54EC ≤ ₹50L); 54F proportionate to net consideration reinvested | 54/54F/54EC |
| Agri integration | tax(normal+agri) − tax(agri+basic exemption) | Finance Act partial integration |
| AMT | 18.5% × (total income + Part‑C/10AA/35AD add‑backs), + surcharge + cess; payable if > regular tax; excess = credit c/f 15 AY; set off when regular > AMT; ATI > ₹20L gate (individual) | 115JC / 115JD / 115JEE |
| 234A/B/C | 1%/month; 234A on (tax − TDS − advance); 234B 90% test; 234C installment‑wise | 234A/B/C |
| Relief 89 | (cur‑year extra tax) − Σ(origin‑year extra tax), floored 0 (Form 10E figures) | 89(1) |
| Relief 90/90A/91 | doubly‑taxed income × min(avg Indian rate, foreign rate), capped at foreign tax | 90/90A/91 |
| Deduction caps | per §3 list, regime‑gated, age‑aware where relevant | Chapter VI‑A |

Full formulas are in the engine source (`backend/src/TallyG.Tax.Domain/TaxEngine/`) and, executably, in the
golden tests.

---

## 6. Worked examples to verify (each carries a full trace in the app + tests)
The **84 golden tests** in `backend/tests/TallyG.Tax.Tests/TaxEngine/` ARE the executable specification —
each has hand‑computed expected figures. Headline cases:
- **Salary ₹20L, old regime** → tax **₹4,13,400** (slab ₹3,97,500 + 4% cess ₹15,900).
- **AMT**: salary ₹50L less 80‑IAC ₹35L → ATI **₹49,50,000**, AMT **₹9,52,380**, regular ₹2,57,400 ⇒ AMT
  payable, **credit c/f ₹6,94,980**.
- **Reliefs**: ₹20L old + relief 89 ₹50k + FTC ₹30k ⇒ **₹3,33,400**.
- **CG**: 112A ₹1.25L exemption; **54EC capped ₹50L**; 54F proportionate.
- **Deductions**: 80U fixed ₹75k (₹1.25L severe); 80G 50% × min(donation, 10% adjusted GTI); 80GG least‑of.
- **Interest**: 234A base excludes self‑assessment tax (regression‑locked).

Reproduce: `dotnet test backend/tests/TallyG.Tax.Tests` (all 84 green at commit `5f74f43`). Or drive any
scenario through `/api/v1/tax/regime-compare` and read the line‑by‑line `trace`.

---

## 7. Open items needing a CA ruling (documented simplifications)
1. **AMT threshold** — we apply the ₹20L individual/HUF/AOP/BOI gate; **firm/LLP have no threshold** — confirm entity handling.
2. **AMT surcharge** — surcharge on AMT computed without marginal relief; confirm acceptable.
3. **Ordering** — FTC (90/91) and relief 89 are applied after the max(regular, AMT) determination; confirm the interaction order.
4. **80GG / 80G base** — "total income / adjusted GTI" approximated as income before those two deductions (excludes special‑rate income); confirm.
5. **CG asset gating** — ✓ **implemented**: s.54EC is now gated to land/building only (ignored for other assets, per `CapitalGainsCalculator`). Confirm, or specify gating for further sections.
6. **234B stop‑date / 234C late‑income proviso** — simplified; confirm.
7. **Current‑year inter‑head set‑off & carry‑forward** — ✓ **implemented** (`LossSetOff`): s.71 inter‑head; s.71(2A) a business loss is NOT set off vs salary; s.71(3A) house‑property inter‑head set‑off capped ₹2,00,000; s.71B/72/73 carry‑forward surfaced; speculative loss ring‑fenced; VDA (115BBH) / casual (115BB) income never reduced. Confirm the absorption ordering (slab‑rate income before special‑rate CG buckets) and that ordinary other‑sources losses correctly lapse.
8. **87A vs special‑rate / agri** — rebate applied against slab tax on normal income only; confirm.
9. **Rounding** — 288A income to ₹10, 288B tax to ₹1; confirm method (round‑half‑up).

---

## 8. Sign‑off block
```
Reviewer (CA):  ____________________    Membership No.: __________
Engine commit reviewed: 5f74f43        Rule-set versions: AY2025-26 v1.0.0 / AY2026-27 v____
Scope reviewed (tick): Salary ☐  HP ☐  CG ☐  Business ☐  Other ☐  Ch.VI-A ☐
                       Slab/87A/surcharge/cess ☐  AMT ☐  Interest 234 ☐  Reliefs 89/90/91 ☐  Set-offs ☐
Open items (§7) ruled on: ☐ all
Findings / corrections (attach):  ____________________________________________
Status:  ☐ Approved   ☐ Approved with corrections (above)   ☐ Not approved
Re-validation: on every Finance Act and any rule-set/engine change. Next review due: __________
Signature: ____________________   Date: __________
```

---

*Maintained by the build team. This pack is regenerated per engine commit / rule‑set version; the figures
remain provisional until the §8 block is signed.*
