'use client';

// ---------------------------------------------------------------------------
// /settings — account settings.
//   • Profile (name editable, email/mobile/PAN shown)
//   • Privacy & consents (DPDP purpose toggles; essential locked)
//   • Language preference (EN / हिंदी)
//   • Security (sign out, data-rights entry point)
// All cards are self-contained feature components from features/settings.
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import {
  ProfileCard,
  ConsentsCard,
  LanguagePreferenceCard,
  SecurityCard,
} from '@/features/settings';
import { BankAccountsCard } from '@/features/bank-accounts';

export default function SettingsPage() {
  const t = useTranslations('settings');

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-ink-900">{t('pageTitle')}</h1>
        <p className="mt-1 text-sm text-ink-500">{t('pageSubtitle')}</p>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <div className="space-y-6">
          <ProfileCard />
          <BankAccountsCard />
          <LanguagePreferenceCard />
        </div>
        <div className="space-y-6">
          <ConsentsCard />
          <SecurityCard />
        </div>
      </div>
    </div>
  );
}
