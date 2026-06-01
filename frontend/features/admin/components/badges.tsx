import { Badge } from '@/components/ui';
import type { AssignmentStatus, LeadStage } from '@/lib/api-types';
import type { UserStatus } from '../types';

type Tone = 'neutral' | 'brand' | 'success' | 'warning' | 'danger' | 'info';

// ---- User account status ----------------------------------------------------
const userStatusTone: Record<UserStatus, Tone> = {
  Active: 'success',
  Locked: 'warning',
  Disabled: 'danger',
  Deleted: 'neutral',
};

export function UserStatusBadge({ status }: { status: UserStatus }) {
  return <Badge tone={userStatusTone[status]}>{status}</Badge>;
}

// ---- Lead funnel stage ------------------------------------------------------
export const leadStageTone: Record<LeadStage, Tone> = {
  New: 'info',
  Contacted: 'brand',
  Qualified: 'warning',
  Converted: 'success',
  Lost: 'neutral',
};

export function LeadStageBadge({ stage }: { stage: LeadStage }) {
  return <Badge tone={leadStageTone[stage]}>{stage}</Badge>;
}

// ---- CA assignment status ---------------------------------------------------
const assignmentTone: Record<AssignmentStatus, Tone> = {
  Unassigned: 'neutral',
  Assigned: 'info',
  InReview: 'warning',
  Completed: 'success',
};

export function AssignmentStatusBadge({ status }: { status: AssignmentStatus }) {
  return <Badge tone={assignmentTone[status]}>{status}</Badge>;
}

// ---- Role chips (read-only display) -----------------------------------------
export function RoleChips({ roles }: { roles: string[] }) {
  if (roles.length === 0) {
    return <span className="text-xs text-ink-400">—</span>;
  }
  return (
    <span className="flex flex-wrap gap-1">
      {roles.map((r) => (
        <Badge key={r} tone={r === 'SuperAdmin' || r === 'Admin' ? 'brand' : 'neutral'}>
          {r}
        </Badge>
      ))}
    </span>
  );
}
