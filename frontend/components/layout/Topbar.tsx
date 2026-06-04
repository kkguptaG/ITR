'use client';

import { useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { useQuery } from '@tanstack/react-query';
import { Bell, ChevronDown, HelpCircle, LogOut, Menu, Settings, User as UserIcon } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useAuth } from '@/lib/auth';
import { getActiveAssessmentYear } from '@/features/returns';
import { formatAssessmentYear } from '@/lib/format';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';

function initials(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase() ?? '')
    .join('');
}

export interface TopbarProps {
  onMenuClick?: () => void;
}

export function Topbar({ onMenuClick }: TopbarProps) {
  const t = useTranslations('topbar');
  const { user, logout } = useAuth();
  const router = useRouter();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const ayQuery = useQuery({ queryKey: ['assessment-year', 'active'], queryFn: getActiveAssessmentYear, staleTime: 60 * 60_000 });
  const activeAy = ayQuery.data?.assessmentYear;

  // Close the user menu on outside click / Escape.
  useEffect(() => {
    if (!menuOpen) return;
    const onClick = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenuOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setMenuOpen(false);
    document.addEventListener('mousedown', onClick);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onClick);
      document.removeEventListener('keydown', onKey);
    };
  }, [menuOpen]);

  async function handleLogout() {
    setMenuOpen(false);
    await logout();
    router.replace('/login');
  }

  return (
    <header className="sticky top-0 z-30 flex h-16 items-center gap-3 border-b border-ink-200 bg-white/80 px-4 backdrop-blur lg:px-6">
      <button
        type="button"
        onClick={onMenuClick}
        aria-label={t('openMenu')}
        className="rounded-lg p-2 text-ink-600 hover:bg-ink-100 lg:hidden"
      >
        <Menu className="h-5 w-5" aria-hidden="true" />
      </button>

      <div className="flex-1" />

      {activeAy && (
        <span className="hidden items-center rounded-full border border-ink-200 bg-ink-50 px-3 py-1 text-xs font-medium text-ink-700 sm:inline-flex">
          {formatAssessmentYear(activeAy)}
        </span>
      )}

      <LanguageSwitcher />

      <Link
        href="/support"
        aria-label={t('help')}
        className="rounded-lg p-2 text-ink-600 hover:bg-ink-100"
      >
        <HelpCircle className="h-5 w-5" aria-hidden="true" />
      </Link>

      <Link
        href="/notices"
        aria-label={t('notifications')}
        className="relative rounded-lg p-2 text-ink-600 hover:bg-ink-100"
      >
        <Bell className="h-5 w-5" aria-hidden="true" />
      </Link>

      <div className="relative" ref={menuRef}>
        <button
          type="button"
          onClick={() => setMenuOpen((v) => !v)}
          aria-haspopup="menu"
          aria-expanded={menuOpen}
          aria-label={t('account')}
          className="flex items-center gap-2 rounded-xl py-1 pl-1 pr-2 hover:bg-ink-100"
        >
          <span className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-100 text-sm font-semibold text-brand-700">
            {user ? initials(user.fullName) : <UserIcon className="h-4 w-4" aria-hidden="true" />}
          </span>
          <span className="hidden text-sm font-medium text-ink-800 sm:block">
            {user?.fullName ?? '—'}
          </span>
          <ChevronDown className="hidden h-4 w-4 text-ink-400 sm:block" aria-hidden="true" />
        </button>

        {menuOpen && (
          <div
            role="menu"
            className="absolute right-0 mt-2 w-56 origin-top-right rounded-xl border border-ink-200 bg-white p-1.5 shadow-card animate-fade-in"
          >
            <div className="px-3 py-2">
              <p className="truncate text-sm font-medium text-ink-900">{user?.fullName}</p>
              <p className="truncate text-xs text-ink-500">{user?.email}</p>
            </div>
            <div className="my-1 h-px bg-ink-100" />
            <Link
              href="/settings"
              role="menuitem"
              onClick={() => setMenuOpen(false)}
              className={cn(
                'flex items-center gap-2 rounded-lg px-3 py-2 text-sm text-ink-700 hover:bg-ink-100',
              )}
            >
              <UserIcon className="h-4 w-4 text-ink-400" aria-hidden="true" />
              {t('profile')}
            </Link>
            <Link
              href="/settings"
              role="menuitem"
              onClick={() => setMenuOpen(false)}
              className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm text-ink-700 hover:bg-ink-100"
            >
              <Settings className="h-4 w-4 text-ink-400" aria-hidden="true" />
              {t('settings')}
            </Link>
            <div className="my-1 h-px bg-ink-100" />
            <button
              type="button"
              role="menuitem"
              onClick={handleLogout}
              className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm text-red-600 hover:bg-red-50"
            >
              <LogOut className="h-4 w-4" aria-hidden="true" />
              {t('logout')}
            </button>
          </div>
        )}
      </div>
    </header>
  );
}
