'use client';

// ---------------------------------------------------------------------------
// /support — the support hub: Tickets · Notices vault · Notifications, as tabs.
// The active tab is reflected in the ?tab= query param so deep links (e.g. the
// notifications bell's "View all") land on the right panel, and the choice
// survives refresh/back. Each panel owns its own data + actions.
// ---------------------------------------------------------------------------

import { Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { Ticket, Bell, BellRing } from 'lucide-react';
import { Tabs, TabsList, TabsTrigger, TabsContent, Spinner } from '@/components/ui';
import { TicketsPanel, NoticesPanel, NotificationsPanel } from '@/features/support';

type TabKey = 'tickets' | 'notices' | 'notifications';
const TABS: TabKey[] = ['tickets', 'notices', 'notifications'];

function SupportTabs() {
  const t = useTranslations('support');
  const router = useRouter();
  const searchParams = useSearchParams();

  const raw = searchParams.get('tab');
  const active: TabKey = TABS.includes(raw as TabKey) ? (raw as TabKey) : 'tickets';

  function setTab(next: string) {
    const params = new URLSearchParams(searchParams.toString());
    params.set('tab', next);
    router.replace(`/support?${params.toString()}`, { scroll: false });
  }

  return (
    <Tabs value={active} onValueChange={setTab}>
      <TabsList>
        <TabsTrigger value="tickets" className="gap-1.5">
          <Ticket className="h-4 w-4" aria-hidden="true" />
          {t('tabTickets')}
        </TabsTrigger>
        <TabsTrigger value="notices" className="gap-1.5">
          <Bell className="h-4 w-4" aria-hidden="true" />
          {t('tabNotices')}
        </TabsTrigger>
        <TabsTrigger value="notifications" className="gap-1.5">
          <BellRing className="h-4 w-4" aria-hidden="true" />
          {t('tabNotifications')}
        </TabsTrigger>
      </TabsList>

      <TabsContent value="tickets">
        <TicketsPanel />
      </TabsContent>
      <TabsContent value="notices">
        <NoticesPanel />
      </TabsContent>
      <TabsContent value="notifications">
        <NotificationsPanel />
      </TabsContent>
    </Tabs>
  );
}

export default function SupportPage() {
  const t = useTranslations('support');
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-ink-900">{t('title')}</h1>
        <p className="text-sm text-ink-500">{t('subtitle')}</p>
      </div>
      <Suspense
        fallback={
          <div className="flex justify-center py-16">
            <Spinner />
          </div>
        }
      >
        <SupportTabs />
      </Suspense>
    </div>
  );
}
