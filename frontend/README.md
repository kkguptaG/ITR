# TallyG Tax — Web (Next.js 14 App Router)

The frontend for the TallyG Tax Portal: a mobile-first, guided ITR-filing experience.
See `docs/architecture/08-frontend-ux.md` for the full UX/design spec.

## Stack

- **Next.js 14** (App Router) + **React 18** + **TypeScript**
- **Tailwind CSS** (fintech theme: indigo + emerald), no Radix/shadcn CLI
- **TanStack Query** (server state) · **react-hook-form** + **zod** (forms)
- **next-intl** (EN/HI) · **axios** (API client) · **lucide-react** (icons)

## Prerequisites

- Node.js **>= 18.18** and npm

## Run locally

```bash
# 1. Install dependencies
npm install

# 2. Configure the API base URL
cp .env.example .env.local
#   NEXT_PUBLIC_API_URL=http://localhost:5080/api/v1   (default backend port)

# 3. Start the dev server (http://localhost:3000)
npm run dev
```

Other scripts:

```bash
npm run build      # production build
npm run start      # serve the production build on :3000
npm run lint       # eslint (next/core-web-vitals)
npm run typecheck  # tsc --noEmit
```

## Project layout (foundation)

```
app/
  layout.tsx            # root: fonts + Providers (i18n, Query, Auth)
  page.tsx              # marketing landing (static)
  loading.tsx           # streaming skeleton
  error.tsx             # route error boundary (maps RFC 7807 problems)
  not-found.tsx         # 404
  providers.tsx         # client provider tree
  globals.css           # Tailwind + theme tokens
  (auth)/layout.tsx     # centered auth shell  (login/register/verify-otp)
  (app)/layout.tsx      # AppShell (Sidebar + Topbar), auth-guarded
  (admin)/layout.tsx    # AppShell, role-gated to Admin/SuperAdmin/Ops
components/
  ui/                   # design system (Button, Input, Field, Card, Stepper, …)
  layout/               # AppShell, Sidebar, Topbar, nav-config
  LanguageSwitcher.tsx
lib/
  api.ts                # axios instance (bearer + refresh-on-401)
  api-types.ts          # TS types mirroring backend DTOs
  auth.tsx              # AuthProvider + useAuth
  token-store.ts        # access (memory) + refresh (localStorage) + session cookie
  format.ts             # INR (lakh/crore) + date formatting
  utils.ts              # cn(), genId()
i18n.ts                 # next-intl request config (locale via cookie)
messages/{en,hi}.json   # translation catalogs
middleware.ts           # edge auth/redirect guard
```

## How feature agents extend this

- **Pages:** add `page.tsx` files under the relevant route group, e.g.
  `app/(app)/returns/page.tsx`, `app/(auth)/login/page.tsx`.
- **Domain components/logic:** add under `features/<area>/`.
- **Design system:** import primitives from `@/components/ui`
  (`import { Button, Card, Field } from '@/components/ui'`).
- **Data:** use TanStack Query with the typed helpers in `@/lib/api`
  (`apiGet`, `apiPost`, …) and types from `@/lib/api-types`.
- **Forms:** `react-hook-form` + `zod` via `@hookform/resolvers/zod`.
- **Auth:** read the user/roles via `useAuth()`; call `login(...)` after the
  OTP-verify response and `logout()` to sign out.
- **i18n:** add keys to `messages/en.json` and `messages/hi.json`; read with
  `useTranslations('<namespace>')`.

### Auth flow (matches the backend contract)

```
POST /auth/register        { fullName, email, mobile }            -> { userId }
POST /auth/otp/request      { identifier, purpose }                -> { otpToken, expiresInSeconds, devOtp }
POST /auth/otp/verify       { otpToken, code }                     -> { accessToken, refreshToken, user }
POST /auth/token/refresh    { refreshToken }                       -> { accessToken, refreshToken }
POST /auth/logout           { refreshToken }                       -> 204
GET  /auth/me                                                      -> user
```

> **Token storage (demo):** access token in memory, refresh token in
> `localStorage`, plus a non-sensitive `tallyg.session` cookie so the Edge
> middleware can guard routes. Production uses an HttpOnly cookie + BFF
> (see `docs/architecture/04-api-and-auth.md` §4.7).

## i18n

Locale-agnostic routing for the demo: the active locale (`en` | `hi`) is read
from the `tallyg.locale` cookie (set by the `LanguageSwitcher`), defaulting to
`en`. Number/currency/date use `en-IN` grouping for both languages.
