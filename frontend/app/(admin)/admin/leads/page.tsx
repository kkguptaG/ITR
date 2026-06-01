'use client';

// ---------------------------------------------------------------------------
// /admin/leads — CRM pipeline (kanban by funnel stage).
//   • GET /admin/leads/pipeline → one column per stage with the latest cards.
//   • "Add lead" opens AddLeadModal (POST /admin/leads).
//   • Clicking a card opens LeadDetailModal: change stage + log activities.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Plus, UserPlus, Mail, Phone } from 'lucide-react';
import { Button, Spinner, Alert, Card } from '@/components/ui';
import type { LeadStage } from '@/lib/api-types';
import { formatRelative } from '@/lib/format';
import {
  PageHeader,
  leadStageTone,
  adminKeys,
  getPipeline,
  type LeadDto,
} from '@/features/admin';
import { AddLeadModal } from '@/features/admin/components/AddLeadModal';
import { LeadDetailModal } from '@/features/admin/components/LeadDetailModal';

// Canonical column order (funnel left→right).
const STAGE_ORDER: LeadStage[] = ['New', 'Contacted', 'Qualified', 'Converted', 'Lost'];

const columnAccent: Record<LeadStage, string> = {
  New: 'bg-sky-400',
  Contacted: 'bg-brand-400',
  Qualified: 'bg-payable-400',
  Converted: 'bg-money-500',
  Lost: 'bg-ink-300',
};

export default function AdminLeadsPage() {
  const [addOpen, setAddOpen] = useState(false);
  const [selected, setSelected] = useState<LeadDto | null>(null);

  const query = useQuery({
    queryKey: adminKeys.leadPipeline(),
    queryFn: () => getPipeline(25),
  });

  // Index returned stages so we can render in canonical order even if the API
  // omits an empty stage.
  const byStage = new Map<LeadStage, { count: number; leads: LeadDto[] }>();
  for (const s of query.data?.stages ?? []) {
    byStage.set(s.stage, { count: s.count, leads: s.leads });
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="CRM pipeline"
        subtitle={
          query.data ? `${query.data.totalLeads} leads in the funnel.` : 'Track prospects through the funnel.'
        }
        actions={
          <Button onClick={() => setAddOpen(true)}>
            <Plus className="h-4 w-4" aria-hidden="true" />
            Add lead
          </Button>
        }
      />

      {query.isLoading ? (
        <div className="flex min-h-[40vh] items-center justify-center">
          <Spinner size={28} label="Loading pipeline…" />
        </div>
      ) : query.isError ? (
        <Alert variant="error">We couldn’t load the pipeline. Please try again.</Alert>
      ) : (
        <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-5">
          {STAGE_ORDER.map((stage) => {
            const col = byStage.get(stage) ?? { count: 0, leads: [] };
            return (
              <div key={stage} className="flex flex-col rounded-2xl bg-ink-50/70 p-3">
                <div className="mb-3 flex items-center justify-between px-1">
                  <span className="flex items-center gap-2 text-sm font-semibold text-ink-800">
                    <span className={`h-2.5 w-2.5 rounded-full ${columnAccent[stage]}`} aria-hidden="true" />
                    {stage}
                  </span>
                  <span className="rounded-full bg-white px-2 py-0.5 text-xs font-medium text-ink-500 ring-1 ring-inset ring-ink-200">
                    {col.count}
                  </span>
                </div>

                <ul className="space-y-2">
                  {col.leads.length === 0 ? (
                    <li className="rounded-xl border border-dashed border-ink-200 px-3 py-6 text-center text-xs text-ink-400">
                      No leads
                    </li>
                  ) : (
                    col.leads.map((lead) => (
                      <li key={lead.id}>
                        <button
                          type="button"
                          onClick={() => setSelected(lead)}
                          className="w-full text-left"
                        >
                          <Card className="p-3 transition-shadow hover:shadow-soft">
                            <p className="truncate font-medium text-ink-900">{lead.name}</p>
                            <div className="mt-1 space-y-0.5 text-xs text-ink-500">
                              {lead.email && (
                                <p className="flex items-center gap-1 truncate">
                                  <Mail className="h-3 w-3" aria-hidden="true" />
                                  {lead.email}
                                </p>
                              )}
                              {lead.mobile && (
                                <p className="flex items-center gap-1">
                                  <Phone className="h-3 w-3" aria-hidden="true" />
                                  {lead.mobile}
                                </p>
                              )}
                            </div>
                            <div className="mt-2 flex items-center justify-between">
                              <span
                                className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium ring-1 ring-inset ${toneClass(
                                  leadStageTone[lead.stage],
                                )}`}
                              >
                                score {lead.score}
                              </span>
                              <span className="text-[11px] text-ink-400">{formatRelative(lead.updatedAt)}</span>
                            </div>
                          </Card>
                        </button>
                      </li>
                    ))
                  )}
                </ul>
              </div>
            );
          })}
        </div>
      )}

      {query.data && query.data.totalLeads === 0 && (
        <div className="rounded-2xl border border-dashed border-ink-300 bg-white px-6 py-10 text-center">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-brand-50 text-brand-600">
            <UserPlus className="h-6 w-6" aria-hidden="true" />
          </div>
          <h3 className="mt-4 text-base font-semibold text-ink-900">No leads yet</h3>
          <p className="mt-1 text-sm text-ink-500">Add your first prospect to start the pipeline.</p>
          <div className="mt-5">
            <Button onClick={() => setAddOpen(true)}>
              <Plus className="h-4 w-4" aria-hidden="true" />
              Add lead
            </Button>
          </div>
        </div>
      )}

      <AddLeadModal open={addOpen} onClose={() => setAddOpen(false)} />
      <LeadDetailModal lead={selected} open={selected !== null} onClose={() => setSelected(null)} />
    </div>
  );
}

// Map the badge tone to ring/bg/text utilities (small local helper for the score chip).
function toneClass(tone: 'neutral' | 'brand' | 'success' | 'warning' | 'danger' | 'info'): string {
  switch (tone) {
    case 'brand':
      return 'bg-brand-50 text-brand-700 ring-brand-200';
    case 'success':
      return 'bg-money-50 text-money-700 ring-money-200';
    case 'warning':
      return 'bg-payable-50 text-payable-700 ring-payable-200';
    case 'danger':
      return 'bg-red-50 text-red-700 ring-red-200';
    case 'info':
      return 'bg-sky-50 text-sky-700 ring-sky-200';
    default:
      return 'bg-ink-100 text-ink-600 ring-ink-200';
  }
}
