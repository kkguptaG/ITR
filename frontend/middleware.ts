import { NextResponse, type NextRequest } from 'next/server';
import { SESSION_COOKIE } from '@/lib/token-store';

// Path prefixes that require authentication. Route groups (app)/(admin) are
// invisible in the URL, so we list the concrete top-level segments here.
const PROTECTED_PREFIXES = [
  '/dashboard',
  '/returns',
  '/refund-tracker',
  '/documents',
  '/payments',
  '/notices',
  '/tickets',
  '/settings',
  '/ca-review',
  '/admin',
  '/onboarding',
];

const AUTH_PAGES = ['/login', '/register', '/verify-otp', '/forgot-password', '/reset-password'];

function isProtected(pathname: string): boolean {
  return PROTECTED_PREFIXES.some((p) => pathname === p || pathname.startsWith(`${p}/`));
}

function isAuthPage(pathname: string): boolean {
  return AUTH_PAGES.some((p) => pathname === p || pathname.startsWith(`${p}/`));
}

/**
 * Edge auth guard (best-effort): redirects unauthenticated users away from
 * protected routes using a non-sensitive presence cookie. The in-memory access
 * token is the real credential and the AppShell does the authoritative client
 * guard + the API enforces RBAC — this just avoids a flash of protected chrome.
 */
export function middleware(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  const hasSession = request.cookies.get(SESSION_COOKIE)?.value === '1';

  if (isProtected(pathname) && !hasSession) {
    const url = request.nextUrl.clone();
    url.pathname = '/login';
    url.search = '';
    url.searchParams.set('next', `${pathname}${search}`);
    return NextResponse.redirect(url);
  }

  // Bounce already-signed-in users away from auth pages to the dashboard.
  if (isAuthPage(pathname) && hasSession) {
    const url = request.nextUrl.clone();
    url.pathname = '/dashboard';
    url.search = '';
    return NextResponse.redirect(url);
  }

  return NextResponse.next();
}

export const config = {
  // Run on everything except Next internals, static assets and API routes.
  matcher: ['/((?!_next/static|_next/image|favicon.ico|robots.txt|sitemap.xml|.*\\..*).*)'],
};
