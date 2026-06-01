import createNextIntlPlugin from 'next-intl/plugin';

// Point next-intl at our request-config module (locale-agnostic single-locale setup for the demo).
const withNextIntl = createNextIntlPlugin('./i18n.ts');

// Security response headers for the browser-facing app (the tier where clickjacking /
// MIME-sniffing / referrer-leak protections actually bite). HSTS is added only in a
// production build so local `next dev`/`next start` over plain HTTP is never HSTS-pinned.
// A tuned Content-Security-Policy is intentionally NOT set here yet — it needs per-app
// work for Next's runtime/inline chunks and a loose CSP gives false assurance.
const securityHeaders = [
  { key: 'X-Content-Type-Options', value: 'nosniff' },
  { key: 'X-Frame-Options', value: 'SAMEORIGIN' },
  { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
  { key: 'Permissions-Policy', value: 'camera=(), microphone=(), geolocation=(), browsing-topics=()' },
];
if (process.env.NODE_ENV === 'production') {
  securityHeaders.push({ key: 'Strict-Transport-Security', value: 'max-age=63072000; includeSubDomains' });
}

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  images: {
    formats: ['image/avif', 'image/webp'],
  },
  async headers() {
    return [{ source: '/:path*', headers: securityHeaders }];
  },
};

export default withNextIntl(nextConfig);
