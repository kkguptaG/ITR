'use client';

// Lead detail drawer (modal) for the CRM pipeline.
//   • Identity + score + current stage
//   • Change stage (PATCH /admin/leads/{id}:stage)
//   • Activity timeline + add an activity (POST /admin/leads/{id}/activities)
// All mutations invalidate the pipeline + this lead's detail.

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Mail, Phone, Clock, MessageSquarePlus } from 'lucide-react';
import {
  Modal,
  Button,
  Select,
  Textarea,
  Input,
  Field,
  Spinner,
  Alert,
} from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatDateTime, formatRelative } from '@/lib/format';
import type { LeadStage } from '@/lib/api-types';
import {
  adminKeys,
  getLead,
  changeLeadStage,
  addLeadActivity,
  LeadStageBadge,
  type LeadDto,
} from '@/features/admin';

const STAGES: LeadStage[] = ['New', 'Contacted', 'Qualified', 'Converted', 'Lost'];

const ACTIVITY_TYPES = [
  { value: 'Call', label: 'Call' },
  { value: 'Email', label: 'Email' },
  { value: 'Meeting', label: 'Meeting' },
  { value: 'Note', label: 'Note' },
  { value: 'WhatsApp', label: 'WhatsApp' },
];

export function LeadDetailModal({
  lead,
  open,
  onClose,
}: {
  lead: LeadDto | null;
  open: boolean;
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const leadId = lead?.id ?? '';

  const detailQuery = useQuery({
    queryKey: adminKeys.leadDetail(leadId),
    queryFn: () => getLead(leadId),
    enabled: open && !!leadId,
  });

  const detail = detailQuery.data;
  const current = detail?.lead ?? lead;

  const [stageDraft, setStageDraft] = useState<LeadStage | ''>('');
  const [activityType, setActivityType] = useState('Note');
  const [activityNotes, setActivityNotes] = useState('');

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: adminKeys.leadDetail(leadId) });
    qc.invalidateQueries({ queryKey: adminKeys.leads() });
  };

  const stageMutation = useMutation({
    mutationFn: (stage: LeadStage) => changeLeadStage(leadId, { stage }),
    onSuccess: () => {
      setStageDraft('');
      invalidate();
    },
  });

  const activityMutation = useMutation({
    mutationFn: () => addLeadActivity(leadId, { type: activityType, notes: activityNotes || null }),
    onSuccess: () => {
      setActivityNotes('');
      invalidate();
    },
  });

  const error =
    (stageMutation.error as ApiError | undefined)?.message ??
    (activityMutation.error as ApiError | undefined)?.message;

  const effectiveStage = stageDraft || current?.stage || 'New';

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="lg"
      title={current?.name ?? 'Lead'}
      description={current ? `Score ${current.score} · ${current.source ?? 'unknown source'}` : undefined}
    >
      {detailQuery.isLoading ? (
        <div className="flex min-h-[160px] items-center justify-center">
          <Spinner label="Loading lead…" />
        </div>
      ) : detailQuery.isError ? (
        <Alert variant="error">Could not load this lead.</Alert>
      ) : (
        <div className="space-y-5">
          {error && <Alert variant="error">{error}</Alert>}

          {/* Contact + stage */}
          <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-sm">
            <span className="flex items-center gap-1.5 text-ink-700">
              <Mail className="h-4 w-4 text-ink-400" aria-hidden="true" />
              {current?.email ?? '—'}
            </span>
            <span className="flex items-center gap-1.5 text-ink-700">
              <Phone className="h-4 w-4 text-ink-400" aria-hidden="true" />
              {current?.mobile ?? '—'}
            </span>
            {current && <LeadStageBadge stage={current.stage} />}
          </div>

          {/* Stage change */}
          <section className="rounded-xl border border-ink-200 p-4">
            <h3 className="text-sm font-semibold text-ink-900">Move stage</h3>
            <div className="mt-3 flex flex-wrap items-end gap-3">
              <Field label="Stage" className="w-44">
                <Select
                  value={effectiveStage}
                  onChange={(e) => setStageDraft(e.target.value as LeadStage)}
                  options={STAGES.map((s) => ({ value: s, label: s }))}
                />
              </Field>
              <Button
                size="sm"
                loading={stageMutation.isPending}
                disabled={!stageDraft || stageDraft === current?.stage}
                onClick={() => stageDraft && stageMutation.mutate(stageDraft)}
              >
                Update stage
              </Button>
            </div>
          </section>

          {/* Add activity */}
          <section className="rounded-xl border border-ink-200 p-4">
            <h3 className="flex items-center gap-1.5 text-sm font-semibold text-ink-900">
              <MessageSquarePlus className="h-4 w-4 text-ink-400" aria-hidden="true" />
              Log activity
            </h3>
            <div className="mt-3 space-y-3">
              <div className="grid gap-3 sm:grid-cols-[10rem_1fr]">
                <Field label="Type">
                  <Select
                    value={activityType}
                    onChange={(e) => setActivityType(e.target.value)}
                    options={ACTIVITY_TYPES}
                  />
                </Field>
                <Field label="Notes">
                  <Input
                    value={activityNotes}
                    onChange={(e) => setActivityNotes(e.target.value)}
                    placeholder="What happened?"
                  />
                </Field>
              </div>
              <div className="flex justify-end">
                <Button
                  size="sm"
                  variant="outline"
                  loading={activityMutation.isPending}
                  onClick={() => activityMutation.mutate()}
                >
                  Add activity
                </Button>
              </div>
            </div>
          </section>

          {/* Timeline */}
          <section>
            <h3 className="text-sm font-semibold text-ink-900">Activity timeline</h3>
            {detail && detail.activities.length > 0 ? (
              <ol className="mt-3 space-y-3">
                {detail.activities.map((a) => (
                  <li key={a.id} className="flex gap-3">
                    <span className="mt-1 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-ink-100 text-ink-500">
                      <Clock className="h-3.5 w-3.5" aria-hidden="true" />
                    </span>
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-ink-800">{a.type}</p>
                      {a.notes && <p className="text-sm text-ink-600">{a.notes}</p>}
                      <p className="text-xs text-ink-400" title={formatDateTime(a.createdAt)}>
                        {formatRelative(a.createdAt)}
                      </p>
                    </div>
                  </li>
                ))}
              </ol>
            ) : (
              <p className="mt-2 text-sm text-ink-400">No activity logged yet.</p>
            )}
          </section>
        </div>
      )}
    </Modal>
  );
}
