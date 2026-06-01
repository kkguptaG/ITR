'use client';

// ReturnStatusBadge — foundation StatusBadge + a translated status label.
// Centralises the messages.status.* lookup so every surface (dashboard, list,
// timeline) shows the same human label and tone for a ReturnStatus.

import { useTranslations } from 'next-intl';
import { StatusBadge } from '@/components/ui';
import type { ReturnStatus } from '@/lib/api-types';

export function ReturnStatusBadge({
  status,
  className,
}: {
  status: ReturnStatus;
  className?: string;
}) {
  const t = useTranslations('status');
  return (
    <StatusBadge status={status} className={className}>
      {t(status)}
    </StatusBadge>
  );
}
