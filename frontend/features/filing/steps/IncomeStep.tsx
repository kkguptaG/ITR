'use client';

// ---------------------------------------------------------------------------
// Step 3 — Income. Conditional income-head editors driven by the ITR type
// (see steps.ts incomeHeads): salary, house property, capital gains, business,
// other sources. Each head is a list with inline add/edit/delete persisted to
// /returns/{id}/<head>. Saving is immediate per row; "Continue" just advances.
// ---------------------------------------------------------------------------

import { useEffect } from 'react';
import { useSearchParams } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui';
import { formatInr } from '@/lib/format';
import { cn } from '@/lib/utils';
import {
  addBusinessIncome,
  addCapitalGain,
  addHouseProperty,
  addIncomeSource,
  addSalary,
  deleteBusinessIncome,
  deleteCapitalGain,
  deleteHouseProperty,
  deleteIncomeSource,
  deleteSalary,
  filingKeys,
  listBusinessIncomes,
  listCapitalGains,
  listHouseProperties,
  listIncomeSources,
  listSalaries,
  updateBusinessIncome,
  updateCapitalGain,
  updateHouseProperty,
  updateIncomeSource,
  updateSalary,
} from '../api';
import type {
  BusinessIncomeDto,
  CapitalGainDto,
  HousePropertyDto,
  IncomeSourceDto,
  SalaryDetailDto,
} from '../types';
import { incomeHeads } from '../steps';
import { useWizard } from '../WizardContext';
import { useInvalidateReturn } from '../useReturn';
import { useHeadCrud } from '../useHeadCrud';
import { WizardStep, WizardFooter } from '../components/WizardStep';
import { EditableList } from '../components/EditableList';
import {
  BusinessIncomeForm,
  CapitalGainForm,
  HousePropertyForm,
  OtherIncomeForm,
  SalaryForm,
} from '../components/income-forms';
import { OTHER_INCOME_NATURES } from '../schemas';
import type { GoodsCarriageVehicle } from '../schemas';

/** Parse the stored goodsCarriageJson into a vehicle array the form can edit (tolerant of bad data). */
function parseGoodsCarriage(json: string | null | undefined): GoodsCarriageVehicle[] {
  if (!json) return [];
  try {
    const arr = JSON.parse(json);
    if (!Array.isArray(arr)) return [];
    return arr.map((v) => ({
      regNo: typeof v.regNo === 'string' ? v.regNo : '',
      ownership: v.ownership === 'LEASE' || v.ownership === 'HIRED' ? v.ownership : 'OWN',
      tonnage: Number(v.tonnage) || 0,
      months: Number(v.months) || 12,
    }));
  } catch {
    return [];
  }
}

export function IncomeStep() {
  const t = useTranslations('wizard');
  const ti = useTranslations('income');
  const tc = useTranslations('common');
  const { returnId, detail, goNext } = useWizard();
  const invalidate = useInvalidateReturn(returnId);
  const heads = incomeHeads(detail.itrType);

  // Deep-link focus: the Computation Dashboard links here with ?focus=<head> to land on a section.
  // The section renders only after the income data settles, and the wizard restores scroll to the
  // top on mount — so a one-shot scroll races and misses. Defer past hydration, then poll a few
  // frames for the section before scrolling it into view.
  const focus = useSearchParams().get('focus');
  useEffect(() => {
    if (!focus) return;
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout>;
    const deadline = Date.now() + 1500;
    // Re-assert an *instant* scroll (a smooth one gets cancelled mid-animation by
    // the router's scroll-restoration) until the section is actually settled at the
    // top, which also covers the section rendering a few frames after navigation.
    const tick = () => {
      if (cancelled) return;
      const el = document.getElementById(`income-${focus}`);
      if (el) {
        if (Math.abs(el.getBoundingClientRect().top) < 8) return; // already in place
        el.scrollIntoView({ behavior: 'auto', block: 'start' });
      }
      if (Date.now() < deadline) timer = setTimeout(tick, 120);
    };
    timer = setTimeout(tick, 80);
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [focus]);

  // ------- salary
  const salary = useHeadCrud<SalaryDetailDto, Parameters<typeof addSalary>[1]>(
    returnId,
    filingKeys.salaries(returnId),
    { list: listSalaries, add: addSalary, update: updateSalary, remove: deleteSalary },
    invalidate,
  );

  // ------- house property
  const house = useHeadCrud<HousePropertyDto, Parameters<typeof addHouseProperty>[1]>(
    returnId,
    filingKeys.houses(returnId),
    { list: listHouseProperties, add: addHouseProperty, update: updateHouseProperty, remove: deleteHouseProperty },
    invalidate,
  );

  // ------- capital gains
  const gains = useHeadCrud<CapitalGainDto, Parameters<typeof addCapitalGain>[1]>(
    returnId,
    filingKeys.gains(returnId),
    { list: listCapitalGains, add: addCapitalGain, update: updateCapitalGain, remove: deleteCapitalGain },
    invalidate,
  );

  // ------- business
  const business = useHeadCrud<BusinessIncomeDto, Parameters<typeof addBusinessIncome>[1]>(
    returnId,
    filingKeys.business(returnId),
    { list: listBusinessIncomes, add: addBusinessIncome, update: updateBusinessIncome, remove: deleteBusinessIncome },
    invalidate,
  );

  // ------- other sources
  const other = useHeadCrud<IncomeSourceDto, Parameters<typeof addIncomeSource>[1]>(
    returnId,
    filingKeys.incomeSources(returnId),
    { list: listIncomeSources, add: addIncomeSource, update: updateIncomeSource, remove: deleteIncomeSource },
    invalidate,
  );

  const otherSources = (other.query.data ?? []).filter((s) => s.type === 'OtherSources');

  return (
    <>
      <WizardStep title={t('incomeTitle')} description={t('incomeSubtitle')}>
        {/* Salary */}
        {heads.salary && (
          <Section id="salary" focused={focus === 'salary'} title={ti('salaryHead')}>
            <EditableList<SalaryDetailDto>
              items={salary.query.data ?? []}
              getKey={(s) => s.id}
              addLabel={ti('addSalary')}
              emptyLabel={ti('noSalary')}
              deleting={salary.deleteMutation.isPending}
              onDelete={(s) => salary.deleteMutation.mutate(s.id)}
              renderSummary={(s) => (
                <div>
                  <div className="font-medium text-ink-800">{s.employer}</div>
                  <div className="text-sm text-ink-500">{ti('grossSalary')}: {formatInr(s.gross)}</div>
                </div>
              )}
              renderForm={(item, done) => (
                <SalaryForm
                  defaultValues={
                    item
                      ? {
                          employer: item.employer,
                          tan: item.tan ?? '',
                          gross: item.gross,
                          hra: item.hra,
                          perquisites: item.perquisites,
                          profitsInLieu: item.profitsInLieu,
                          exemptAllowances: item.exemptAllowances,
                          hraExemption: item.hraExemption,
                          stdDeduction: item.stdDeduction,
                          professionalTax: item.professionalTax,
                          components: (item.components ?? []).map((c) => ({
                            label: c.label,
                            category: c.category,
                            total: c.total,
                            exempt: c.exempt,
                            isHra: c.isHra,
                          })),
                        }
                      : undefined
                  }
                  loading={salary.addMutation.isPending || salary.updateMutation.isPending}
                  onCancel={done}
                  onSubmit={(v) => {
                    const body = { ...v, tan: v.tan || null };
                    const op = item
                      ? salary.updateMutation.mutateAsync({ id: item.id, body })
                      : salary.addMutation.mutateAsync(body);
                    void op.then(done);
                  }}
                />
              )}
            />
          </Section>
        )}

        {/* House property */}
        {heads.houseProperty && (
          <Section id="house" focused={focus === 'house'} title={ti('houseHead')}>
            <EditableList<HousePropertyDto>
              items={house.query.data ?? []}
              getKey={(h) => h.id}
              addLabel={ti('addHouse')}
              emptyLabel={ti('noHouse')}
              maxOneReached={heads.singleHouseProperty && (house.query.data?.length ?? 0) >= 1}
              deleting={house.deleteMutation.isPending}
              onDelete={(h) => house.deleteMutation.mutate(h.id)}
              renderSummary={(h) => (
                <div>
                  <div className="font-medium text-ink-800">{ti(houseTypeKey(h.type))}</div>
                  <div className="text-sm text-ink-500">{ti('netIncome')}: {formatInr(h.netIncome)}</div>
                </div>
              )}
              renderForm={(item, done) => (
                <HousePropertyForm
                  defaultValues={
                    item
                      ? {
                          type: item.type,
                          address: item.address ?? '',
                          annualValue: item.annualValue,
                          annualRent: item.annualRent,
                          municipalTaxPaid: item.municipalTaxPaid,
                          interestOnLoan: item.interestOnLoan,
                          coOwnerSharePct: item.coOwnerSharePct,
                        }
                      : undefined
                  }
                  loading={house.addMutation.isPending || house.updateMutation.isPending}
                  onCancel={done}
                  onSubmit={(v) => {
                    const body = { ...v, address: v.address || null };
                    const op = item
                      ? house.updateMutation.mutateAsync({ id: item.id, body })
                      : house.addMutation.mutateAsync(body);
                    void op.then(done);
                  }}
                />
              )}
            />
          </Section>
        )}

        {/* Capital gains */}
        {heads.capitalGains && (
          <Section id="capitalGains" focused={focus === 'capitalGains'} title={ti('capitalGainsHead')}>
            <EditableList<CapitalGainDto>
              items={gains.query.data ?? []}
              getKey={(g) => g.id}
              addLabel={ti('addCapitalGain')}
              emptyLabel={ti('noCapitalGains')}
              deleting={gains.deleteMutation.isPending}
              onDelete={(g) => gains.deleteMutation.mutate(g.id)}
              renderSummary={(g) => (
                <div>
                  <div className="font-medium text-ink-800">
                    {ti(`asset.${g.assetType}`)} · {g.term === 'Long' ? ti('longTerm') : ti('shortTerm')}
                  </div>
                  <div className="text-sm text-ink-500">{ti('gain')}: {formatInr(g.gain)}</div>
                </div>
              )}
              renderForm={(item, done) => (
                <CapitalGainForm
                  defaultValues={
                    item
                      ? {
                          assetType: item.assetType,
                          term: item.term,
                          acquisitionMode: item.acquisitionMode ?? 'Purchase',
                          acquisitionDate: item.acquisitionDate ?? '',
                          transferDate: item.transferDate ?? '',
                          previousOwnerAcquisitionDate: item.previousOwnerAcquisitionDate ?? '',
                          previousOwnerCost: item.previousOwnerCost ?? 0,
                          isRuralAgriculturalLand: item.isRuralAgriculturalLand ?? false,
                          exemptUnderDtaa: item.exemptUnderDtaa ?? false,
                          salePrice: item.salePrice,
                          costOfAcquisition: item.costOfAcquisition,
                          costOfImprovement: item.costOfImprovement,
                          improvementDate: item.improvementDate ?? '',
                          expensesOnTransfer: item.expensesOnTransfer,
                          exemptionAmount: item.exemptionAmount,
                          exemptionSection: item.exemptionSection ?? '',
                          reinvestmentAmount: item.reinvestmentAmount,
                          fairMarketValue31Jan2018: item.fairMarketValue31Jan2018,
                          lots: (item.lots ?? []).map((l) => ({
                            acquisitionDate: l.acquisitionDate ?? '',
                            quantity: l.quantity,
                            cost: l.cost,
                            fairMarketValue31Jan2018: l.fairMarketValue31Jan2018,
                          })),
                        }
                      : undefined
                  }
                  loading={gains.addMutation.isPending || gains.updateMutation.isPending}
                  onCancel={done}
                  onSubmit={(v) => {
                    const body = {
                      ...v,
                      acquisitionDate: v.acquisitionDate || null,
                      transferDate: v.transferDate || null,
                      improvementDate: v.improvementDate || null,
                      previousOwnerAcquisitionDate: v.previousOwnerAcquisitionDate || null,
                      lots: (v.lots ?? [])
                        .filter((l) => (Number(l.quantity) || 0) > 0)
                        .map((l) => ({ ...l, acquisitionDate: l.acquisitionDate || null })),
                    };
                    const op = item
                      ? gains.updateMutation.mutateAsync({ id: item.id, body })
                      : gains.addMutation.mutateAsync(body);
                    void op.then(done);
                  }}
                />
              )}
            />
          </Section>
        )}

        {/* Business income */}
        {heads.business && (
          <Section id="business" focused={focus === 'business'} title={ti('businessHead')}>
            <EditableList<BusinessIncomeDto>
              items={business.query.data ?? []}
              getKey={(b) => b.id}
              addLabel={ti('addBusiness')}
              emptyLabel={ti('noBusiness')}
              deleting={business.deleteMutation.isPending}
              onDelete={(b) => business.deleteMutation.mutate(b.id)}
              renderSummary={(b) => (
                <div>
                  <div className="font-medium text-ink-800">
                    {b.isPresumptive ? `${ti('presumptive')} ${b.presumptiveSection ?? ''}` : ti('regularBooks')}
                  </div>
                  <div className="text-sm text-ink-500">
                    {ti('turnover')}: {formatInr(b.turnover)} · {ti('netProfit')}: {formatInr(b.netProfit)}
                  </div>
                </div>
              )}
              renderForm={(item, done) => (
                <BusinessIncomeForm
                  presumptiveOnly={heads.businessPresumptiveOnly}
                  defaultValues={
                    item
                      ? {
                          isPresumptive: item.isPresumptive,
                          presumptiveSection: (item.presumptiveSection as '44AD' | '44ADA' | '44AE') ?? '44AD',
                          natureOfBusinessCode: item.natureOfBusinessCode ?? '',
                          accountingMethod: (item.accountingMethod as 'mercantile' | 'cash') ?? 'mercantile',
                          turnover: item.turnover,
                          grossReceiptsDigital: item.grossReceiptsDigital,
                          grossReceiptsCash: item.grossReceiptsCash,
                          netProfit: item.netProfit,
                          speculativeFlag: item.speculativeFlag,
                          gstTurnoverReported: item.gstTurnoverReported,
                          partnerCapital: item.partnerCapital,
                          securedLoans: item.securedLoans,
                          unsecuredLoans: item.unsecuredLoans,
                          sundryCreditors: item.sundryCreditors,
                          fixedAssets: item.fixedAssets,
                          inventory: item.inventory,
                          sundryDebtors: item.sundryDebtors,
                          bankBalance: item.bankBalance,
                          cashBalance: item.cashBalance,
                          goodsCarriage: parseGoodsCarriage(item.goodsCarriageJson),
                        }
                      : undefined
                  }
                  loading={business.addMutation.isPending || business.updateMutation.isPending}
                  onCancel={done}
                  onSubmit={(v) => {
                    // Fold the vehicle array into goodsCarriageJson; drop the array from the request body.
                    const { goodsCarriage, ...rest } = v;
                    const body = {
                      ...rest,
                      presumptiveSection: v.isPresumptive ? v.presumptiveSection : null,
                      goodsCarriageJson: JSON.stringify(goodsCarriage ?? []),
                    };
                    const op = item
                      ? business.updateMutation.mutateAsync({ id: item.id, body })
                      : business.addMutation.mutateAsync(body);
                    void op.then(done);
                  }}
                />
              )}
            />
          </Section>
        )}

        {/* Other sources */}
        {heads.otherSources && (
          <Section id="other" focused={focus === 'other'} title={ti('otherHead')}>
            <EditableList<IncomeSourceDto>
              items={otherSources}
              getKey={(s) => s.id}
              addLabel={ti('addOther')}
              emptyLabel={ti('noOther')}
              deleting={other.deleteMutation.isPending}
              onDelete={(s) => other.deleteMutation.mutate(s.id)}
              renderSummary={(s) => (
                <div>
                  <div className="font-medium text-ink-800">{s.label || ti('otherHead')}</div>
                  <div className="text-sm text-ink-500">{formatInr(s.amount)}</div>
                </div>
              )}
              renderForm={(item, done) => (
                <OtherIncomeForm
                  defaultValues={item ? { label: item.label ?? '', amount: item.amount, nature: parseNature(item.sourceMetaJson) } : undefined}
                  loading={other.addMutation.isPending || other.updateMutation.isPending}
                  onCancel={done}
                  onSubmit={(v) => {
                    const body = {
                      type: 'OtherSources' as const,
                      label: v.label,
                      amount: v.amount,
                      sourceMetaJson: v.nature && v.nature !== 'normal' ? JSON.stringify({ nature: v.nature }) : null,
                    };
                    const op = item
                      ? other.updateMutation.mutateAsync({ id: item.id, body })
                      : other.addMutation.mutateAsync(body);
                    void op.then(done);
                  }}
                />
              )}
            />
          </Section>
        )}
      </WizardStep>

      <WizardFooter
        primary={
          <Button type="button" onClick={goNext}>
            {tc('continue')}
          </Button>
        }
      />
    </>
  );
}

function Section({ id, title, focused, children }: { id?: string; title: string; focused?: boolean; children: React.ReactNode }) {
  return (
    <section
      id={id ? `income-${id}` : undefined}
      className={cn(
        'space-y-3 scroll-mt-24 rounded-xl',
        focused && 'animate-[pulse_1.2s_ease-in-out_2] ring-2 ring-brand-300 ring-offset-4 ring-offset-bg',
      )}
    >
      <h3 className="text-sm font-semibold uppercase tracking-wide text-ink-500">{title}</h3>
      {children}
    </section>
  );
}

function houseTypeKey(type: HousePropertyDto['type']): string {
  switch (type) {
    case 'LetOut':
      return 'letOut';
    case 'DeemedLetOut':
      return 'deemedLetOut';
    default:
      return 'selfOccupied';
  }
}

type OtherNature = (typeof OTHER_INCOME_NATURES)[number];

/** Read the {"nature":"…"} tag back out of an income source's sourceMetaJson for the edit form. */
function parseNature(metaJson: string | null | undefined): OtherNature {
  if (!metaJson) return 'normal';
  try {
    const n = (JSON.parse(metaJson) as { nature?: string })?.nature;
    return (OTHER_INCOME_NATURES as readonly string[]).includes(n ?? '') ? (n as OtherNature) : 'normal';
  } catch {
    return 'normal';
  }
}
