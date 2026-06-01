import createNextIntlPlugin from 'next-intl/plugin';

// Point next-intl at our request-config module (locale-agnostic single-locale setup for the demo).
const withNextIntl = createNextIntlPlugin('./i18n.ts');

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  images: {
    formats: ['image/avif', 'image/webp'],
  },
};

export default withNextIntl(nextConfig);
