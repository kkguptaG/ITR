'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { ShieldCheck, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useAuth } from '@/lib/auth';
import type { Role } from '@/lib/api-types';
import { navSections, type NavItem } from './nav-config';

function hasAccess(roles: Role[] | undefined, userRoles: Role[]): boolean {
  if (!roles || roles.length === 0) return true;
  return roles.some((r) => userRoles.includes(r));
}

function isActive(pathname: string, href: string): boolean {
  if (href === '/dashboard' || href === '/admin') return pathname === href;
  return pathname === href || pathname.startsWith(`${href}/`);
}

export interface SidebarProps {
  /** Mobile drawer open state (controlled by AppShell). */
  open?: boolean;
  onClose?: () => void;
}

export function Sidebar({ open = false, onClose }: SidebarProps) {
  const t = useTranslations('nav');
  const tApp = useTranslations('common');
  const pathname = usePathname();
  const { roles } = useAuth();

  const renderItem = (item: NavItem) => {
    if (!hasAccess(item.roles, roles)) return null;
    const active = isActive(pathname, item.href);
    const Icon = item.icon;
    return (
      <li key={item.href}>
        <Link
          href={item.href}
          onClick={onClose}
          aria-current={active ? 'page' : undefined}
          className={cn(
            'flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors',
            active
              ? 'bg-brand-50 text-brand-700'
              : 'text-ink-600 hover:bg-ink-100 hover:text-ink-900',
          )}
        >
          <Icon className={cn('h-5 w-5 shrink-0', active ? 'text-brand-600' : 'text-ink-400')} aria-hidden="true" />
          {t(item.messageKey)}
        </Link>
      </li>
    );
  };

  const content = (
    <div className="flex h-full flex-col">
      {/* Brand */}
      <div className="flex h-16 items-center justify-between px-5">
        <Link href="/dashboard" className="flex items-center gap-2" onClick={onClose}>
          <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-brand-600 text-white">
            <ShieldCheck className="h-5 w-5" aria-hidden="true" />
          </span>
          <span className="text-base font-semibold text-ink-900">{tApp('appName')}</span>
        </Link>
        {onClose && (
          <button
            type="button"
            onClick={onClose}
            aria-label="Close menu"
            className="rounded-lg p-1.5 text-ink-400 hover:bg-ink-100 lg:hidden"
          >
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        )}
      </div>

      <nav className="scrollbar-thin flex-1 space-y-6 overflow-y-auto px-3 py-2" aria-label="Main">
        {navSections.map((section, idx) => {
          if (!hasAccess(section.roles, roles)) return null;
          const visibleItems = section.items.filter((i) => hasAccess(i.roles, roles));
          if (visibleItems.length === 0) return null;
          return (
            <div key={idx}>
              {section.titleKey && (
                <p className="px-3 pb-1.5 text-xs font-semibold uppercase tracking-wider text-ink-400">
                  {t(section.titleKey)}
                </p>
              )}
              <ul className="space-y-1">{visibleItems.map(renderItem)}</ul>
            </div>
          );
        })}
      </nav>

      <div className="border-t border-ink-100 p-4">
        <div className="flex items-center gap-2 rounded-xl bg-money-50 px-3 py-2 text-xs text-money-700">
          <ShieldCheck className="h-4 w-4" aria-hidden="true" />
          <span>256-bit encrypted · Data in India</span>
        </div>
      </div>
    </div>
  );

  return (
    <>
      {/* Desktop persistent sidebar */}
      <aside className="hidden w-64 shrink-0 border-r border-ink-200 bg-white lg:block">
        {content}
      </aside>

      {/* Mobile drawer */}
      <div
        className={cn(
          'fixed inset-0 z-40 lg:hidden',
          open ? 'pointer-events-auto' : 'pointer-events-none',
        )}
        aria-hidden={!open}
      >
        <div
          className={cn(
            'absolute inset-0 bg-ink-900/40 transition-opacity',
            open ? 'opacity-100' : 'opacity-0',
          )}
          onClick={onClose}
        />
        <aside
          className={cn(
            'absolute left-0 top-0 h-full w-72 max-w-[85%] bg-white shadow-card transition-transform',
            open ? 'translate-x-0' : '-translate-x-full',
          )}
        >
          {content}
        </aside>
      </div>
    </>
  );
}
