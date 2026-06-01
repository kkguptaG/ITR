import type { Metadata, Viewport } from 'next';
import { Inter } from 'next/font/google';
import { getLocale, getMessages } from 'next-intl/server';
import { Providers } from './providers';
import './globals.css';

// Self-hosted Inter via next/font (display: swap, stable CLS — see doc 8.9).
const inter = Inter({
  subsets: ['latin'],
  variable: '--font-sans',
  display: 'swap',
});

export const metadata: Metadata = {
  title: {
    default: 'TallyG Tax — File your ITR online',
    template: '%s · TallyG Tax',
  },
  description:
    'File your Income Tax Return online — simply and securely. Upload Form 16, compare old vs new regime, and e-file with confidence. Built for India.',
  applicationName: 'TallyG Tax',
};

export const viewport: Viewport = {
  themeColor: '#4f46e5',
  width: 'device-width',
  initialScale: 1,
};

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  // Resolve locale + messages on the server (next-intl reads the locale cookie).
  const locale = await getLocale();
  const messages = await getMessages();
  const now = new Date().toISOString();

  return (
    <html lang={locale} className={inter.variable} suppressHydrationWarning>
      <body className="min-h-screen bg-ink-50 font-sans text-ink-900">
        <Providers locale={locale} messages={messages} now={now}>
          {children}
        </Providers>
      </body>
    </html>
  );
}
