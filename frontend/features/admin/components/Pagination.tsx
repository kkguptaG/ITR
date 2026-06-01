'use client';

import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button, Card } from '@/components/ui';

/**
 * Shared pager for the admin paged tables. Renders nothing when there is only
 * one page. `total` is the row count; page size is supplied to compute pages.
 */
export function Pagination({
  page,
  pageSize,
  total,
  isFetching,
  onPageChange,
}: {
  page: number;
  pageSize: number;
  total: number;
  isFetching?: boolean;
  onPageChange: (page: number) => void;
}) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  if (totalPages <= 1) return null;

  return (
    <Card className="flex items-center justify-between p-3">
      <Button
        variant="ghost"
        size="sm"
        disabled={page <= 1 || isFetching}
        onClick={() => onPageChange(Math.max(1, page - 1))}
      >
        <ChevronLeft className="h-4 w-4" aria-hidden="true" />
        Previous
      </Button>
      <span className="text-sm text-ink-500">
        Page {page} of {totalPages}
      </span>
      <Button
        variant="ghost"
        size="sm"
        disabled={page >= totalPages || isFetching}
        onClick={() => onPageChange(Math.min(totalPages, page + 1))}
      >
        Next
        <ChevronRight className="h-4 w-4" aria-hidden="true" />
      </Button>
    </Card>
  );
}
