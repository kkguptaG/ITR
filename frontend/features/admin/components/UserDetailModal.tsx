'use client';

// User detail / edit modal for the admin users board.
//   • Shows identity, verification, activity counters
//   • Change account status (Active / Locked / Disabled) — PATCH :status
//   • Assign / remove roles — POST /roles
// Mutations invalidate the user list + this user's detail so the table refreshes.

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Mail, Phone, ShieldCheck, X, Plus } from 'lucide-react';
import {
  Modal,
  Button,
  Select,
  Spinner,
  Alert,
  Badge,
  Field,
} from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatDateTime, formatRelative } from '@/lib/format';
import type { Role } from '@/lib/api-types';
import {
  adminKeys,
  getUser,
  setUserStatus,
  modifyUserRole,
  type AdminUserListItemDto,
  type UserStatus,
} from '@/features/admin';

// Roles that can be granted from this screen (subset of the system roles).
const ASSIGNABLE_ROLES: Role[] = [
  'User',
  'CA',
  'CaFirmAdmin',
  'Reviewer',
  'Ops',
  'Admin',
  'Affiliate',
];

const STATUS_OPTIONS: { value: UserStatus; label: string }[] = [
  { value: 'Active', label: 'Active' },
  { value: 'Locked', label: 'Locked' },
  { value: 'Disabled', label: 'Disabled' },
];

export function UserDetailModal({
  user,
  open,
  onClose,
}: {
  user: AdminUserListItemDto | null;
  open: boolean;
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const userId = user?.id ?? '';

  // Fetch the full detail (activity counters) when the modal opens.
  const detailQuery = useQuery({
    queryKey: adminKeys.userDetail(userId),
    queryFn: () => getUser(userId),
    enabled: open && !!userId,
  });

  const detail = detailQuery.data;
  const roles = detail?.roles ?? user?.roles ?? [];

  const [statusDraft, setStatusDraft] = useState<UserStatus | ''>('');
  const [roleToAdd, setRoleToAdd] = useState<string>('');

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: adminKeys.userDetail(userId) });
    qc.invalidateQueries({ queryKey: adminKeys.users() });
  };

  const statusMutation = useMutation({
    mutationFn: (status: UserStatus) => setUserStatus(userId, { status }),
    onSuccess: () => {
      setStatusDraft('');
      invalidate();
    },
  });

  const roleMutation = useMutation({
    mutationFn: (vars: { role: string; action: 'assign' | 'remove' }) =>
      modifyUserRole(userId, vars),
    onSuccess: () => {
      setRoleToAdd('');
      invalidate();
    },
  });

  const mutationError =
    (statusMutation.error as ApiError | undefined)?.message ??
    (roleMutation.error as ApiError | undefined)?.message;

  const assignableRemaining = ASSIGNABLE_ROLES.filter((r) => !roles.includes(r));
  const currentStatus = (detail?.status ?? user?.status) as UserStatus | undefined;
  const effectiveStatus = statusDraft || currentStatus || 'Active';

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="lg"
      title={user?.fullName ?? 'User'}
      description={user ? `Manage account, status and roles` : undefined}
    >
      {detailQuery.isLoading ? (
        <div className="flex min-h-[160px] items-center justify-center">
          <Spinner label="Loading user…" />
        </div>
      ) : detailQuery.isError ? (
        <Alert variant="error">Could not load this user.</Alert>
      ) : (
        <div className="space-y-5">
          {mutationError && <Alert variant="error">{mutationError}</Alert>}

          {/* Identity */}
          <div className="grid gap-3 sm:grid-cols-2">
            <InfoRow icon={Mail} label="Email" value={detail?.email ?? '—'} verified={detail?.emailVerified} />
            <InfoRow icon={Phone} label="Mobile" value={detail?.mobile ?? '—'} verified={detail?.mobileVerified} />
            <InfoRow label="PAN" value={detail?.panMasked ?? '—'} />
            <InfoRow label="Joined" value={formatDateTime(detail?.createdAt)} />
            <InfoRow label="Last login" value={detail?.lastLoginAt ? formatRelative(detail.lastLoginAt) : 'Never'} />
            <InfoRow label="Activity" value={`${detail?.returnsCount ?? 0} returns · ${detail?.paymentsCount ?? 0} payments`} />
          </div>

          {/* Status */}
          <section className="rounded-xl border border-ink-200 p-4">
            <h3 className="text-sm font-semibold text-ink-900">Account status</h3>
            <p className="mt-0.5 text-xs text-ink-500">
              Locking signs the user out; disabling blocks new sign-ins.
            </p>
            <div className="mt-3 flex flex-wrap items-end gap-3">
              <Field label="Status" className="w-44">
                <Select
                  value={effectiveStatus}
                  options={STATUS_OPTIONS}
                  onChange={(e) => setStatusDraft(e.target.value as UserStatus)}
                />
              </Field>
              <Button
                size="sm"
                loading={statusMutation.isPending}
                disabled={!statusDraft || statusDraft === currentStatus}
                onClick={() => statusDraft && statusMutation.mutate(statusDraft)}
              >
                Update status
              </Button>
            </div>
          </section>

          {/* Roles */}
          <section className="rounded-xl border border-ink-200 p-4">
            <h3 className="flex items-center gap-1.5 text-sm font-semibold text-ink-900">
              <ShieldCheck className="h-4 w-4 text-ink-400" aria-hidden="true" />
              Roles
            </h3>
            <div className="mt-3 flex flex-wrap gap-2">
              {roles.length === 0 && <span className="text-sm text-ink-400">No roles assigned.</span>}
              {roles.map((r) => (
                <span
                  key={r}
                  className="inline-flex items-center gap-1 rounded-full bg-ink-100 py-0.5 pl-2.5 pr-1 text-xs font-medium text-ink-700 ring-1 ring-inset ring-ink-200"
                >
                  {r}
                  <button
                    type="button"
                    aria-label={`Remove ${r}`}
                    disabled={roleMutation.isPending}
                    onClick={() => roleMutation.mutate({ role: r, action: 'remove' })}
                    className="rounded-full p-0.5 text-ink-400 hover:bg-ink-200 hover:text-ink-700 disabled:opacity-50"
                  >
                    <X className="h-3 w-3" aria-hidden="true" />
                  </button>
                </span>
              ))}
            </div>
            <div className="mt-3 flex flex-wrap items-end gap-3">
              <Field label="Add role" className="w-44">
                <Select
                  value={roleToAdd}
                  placeholder="Select role…"
                  onChange={(e) => setRoleToAdd(e.target.value)}
                  options={assignableRemaining.map((r) => ({ value: r, label: r }))}
                />
              </Field>
              <Button
                size="sm"
                variant="outline"
                loading={roleMutation.isPending}
                disabled={!roleToAdd}
                onClick={() => roleToAdd && roleMutation.mutate({ role: roleToAdd, action: 'assign' })}
              >
                <Plus className="h-4 w-4" aria-hidden="true" />
                Assign
              </Button>
            </div>
          </section>
        </div>
      )}
    </Modal>
  );
}

function InfoRow({
  icon: Icon,
  label,
  value,
  verified,
}: {
  icon?: typeof Mail;
  label: string;
  value: string;
  verified?: boolean;
}) {
  return (
    <div>
      <p className="text-xs font-medium uppercase tracking-wide text-ink-400">{label}</p>
      <p className="mt-0.5 flex items-center gap-1.5 text-sm text-ink-800">
        {Icon && <Icon className="h-3.5 w-3.5 text-ink-400" aria-hidden="true" />}
        <span className="truncate">{value}</span>
        {verified !== undefined && value !== '—' && (
          <Badge tone={verified ? 'success' : 'neutral'}>{verified ? 'Verified' : 'Unverified'}</Badge>
        )}
      </p>
    </div>
  );
}
