'use client';

// ---------------------------------------------------------------------------
// CouponField — apply/clear a coupon against a plan's price.
//   POST /coupons:validate {code, planCode} → { valid, discountAmount, netAmount }
// On a valid coupon it reports the code upward (the parent re-prices the order
// with couponCode so the SERVER applies the discount authoritatively). Used
// inside CheckoutDialog and as the standalone "apply coupon" box on /payments.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useTranslations } from 'next-intl';
import { Tag, Check, X } from 'lucide-react';
import { Input, Button, Alert } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatInr } from '@/lib/format';
import { validateCoupon } from '../api';
import type { CouponResultDto } from '../types';

export interface CouponFieldProps {
  planCode: string;
  /** Called with the validated code so the parent can re-price with it. */
  onApplied?: (code: string, result: CouponResultDto) => void;
  /** Called when the user removes an applied coupon. */
  onCleared?: () => void;
  className?: string;
}

export function CouponField({ planCode, onApplied, onCleared, className }: CouponFieldProps) {
  const t = useTranslations('payments');
  const tc = useTranslations('common');

  const [code, setCode] = useState('');
  const [applied, setApplied] = useState<CouponResultDto | null>(null);

  const validate = useMutation({
    mutationFn: (raw: string) => validateCoupon({ code: raw.trim(), planCode }),
    onSuccess: (res) => {
      if (res.valid) {
        setApplied(res);
        onApplied?.(res.code, res);
      }
    },
  });

  const clear = () => {
    setApplied(null);
    setCode('');
    validate.reset();
    onCleared?.();
  };

  const invalidMessage =
    validate.error instanceof ApiError
      ? (validate.error.problem.detail ?? validate.error.message)
      : validate.isSuccess && validate.data && !validate.data.valid
        ? (validate.data.message ?? 'This coupon is not valid.')
        : null;

  if (applied) {
    return (
      <div className={className}>
        <div className="flex items-center justify-between rounded-xl border border-money-200 bg-money-50 px-3.5 py-3 text-sm">
          <span className="flex items-center gap-2 font-medium text-money-800">
            <Check className="h-4 w-4" aria-hidden="true" />
            {applied.code} applied — you save {formatInr(applied.discountAmount)}
          </span>
          <button
            type="button"
            onClick={clear}
            aria-label="Remove coupon"
            className="rounded-lg p-1 text-money-700 hover:bg-money-100"
          >
            <X className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className={className}>
      <label htmlFor="coupon-code" className="mb-1.5 block text-sm font-medium text-ink-700">
        {t('coupon')}
      </label>
      <div className="flex gap-2">
        <div className="relative flex-1">
          <Tag
            className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-400"
            aria-hidden="true"
          />
          <Input
            id="coupon-code"
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            placeholder="SAVE20"
            className="pl-9 uppercase"
            invalid={!!invalidMessage}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && code.trim()) {
                e.preventDefault();
                validate.mutate(code);
              }
            }}
          />
        </div>
        <Button
          variant="outline"
          onClick={() => validate.mutate(code)}
          loading={validate.isPending}
          disabled={!code.trim()}
        >
          {tc('apply')}
        </Button>
      </div>
      {invalidMessage && (
        <Alert variant="error" className="mt-2">
          {invalidMessage}
        </Alert>
      )}
    </div>
  );
}
