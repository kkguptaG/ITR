'use client';

// ---------------------------------------------------------------------------
// SecurityCard — session + data-rights actions.
//   • Sign out (revokes the refresh token server-side, clears local state).
//   • DPDP data-rights note: links to support for access/correction/erasure
//     requests (the DSAR workflow lives in the support/ops queue, docs 06).
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { LogOut, ShieldQuestion, ChevronRight, LifeBuoy } from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  Button,
} from '@/components/ui';
import { useAuth } from '@/lib/auth';

export function SecurityCard() {
  const t = useTranslations('settings');
  const { logout } = useAuth();
  const router = useRouter();
  const [busy, setBusy] = useState(false);

  async function handleLogout() {
    setBusy(true);
    await logout();
    router.replace('/login');
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('securityTitle')}</CardTitle>
        <CardDescription>{t('securitySubtitle')}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <Link
          href="/help"
          className="flex items-center justify-between gap-3 rounded-xl border border-ink-200 px-3.5 py-3 transition-colors hover:bg-ink-50"
        >
          <span className="flex items-center gap-3">
            <LifeBuoy className="h-5 w-5 shrink-0 text-ink-400" aria-hidden="true" />
            <span className="min-w-0">
              <span className="block text-sm font-medium text-ink-900">{t('helpTitle')}</span>
              <span className="block text-xs text-ink-500">{t('helpBody')}</span>
            </span>
          </span>
          <ChevronRight className="h-4 w-4 shrink-0 text-ink-400" aria-hidden="true" />
        </Link>

        <Link
          href="/tickets"
          className="flex items-center justify-between gap-3 rounded-xl border border-ink-200 px-3.5 py-3 transition-colors hover:bg-ink-50"
        >
          <span className="flex items-center gap-3">
            <ShieldQuestion className="h-5 w-5 shrink-0 text-ink-400" aria-hidden="true" />
            <span className="min-w-0">
              <span className="block text-sm font-medium text-ink-900">{t('dataRightsTitle')}</span>
              <span className="block text-xs text-ink-500">{t('dataRightsBody')}</span>
            </span>
          </span>
          <ChevronRight className="h-4 w-4 shrink-0 text-ink-400" aria-hidden="true" />
        </Link>

        <Button variant="outline" onClick={handleLogout} loading={busy} fullWidth>
          <LogOut className="h-4 w-4" aria-hidden="true" />
          {t('signOut')}
        </Button>
      </CardContent>
    </Card>
  );
}
