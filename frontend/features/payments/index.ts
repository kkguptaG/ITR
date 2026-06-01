// Barrel for the Payments / Wallet / Coupons feature.
// The CheckoutDialog + CouponField are explicitly part of the public surface so
// the filing wizard can reuse the checkout: import { CheckoutDialog } from
// '@/features/payments'.
export { CheckoutDialog, type CheckoutDialogProps } from './components/CheckoutDialog';
export { CouponField, type CouponFieldProps } from './components/CouponField';
export { PaymentHistoryTable } from './components/PaymentHistoryTable';
export { WalletPanel } from './components/WalletPanel';
export * from './api';
export type * from './types';
