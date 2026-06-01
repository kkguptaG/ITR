import { Spinner } from '@/components/ui/Spinner';

/** Streaming fallback while a route segment loads (doc 8.5.5). */
export default function Loading() {
  return (
    <div className="flex min-h-[60vh] items-center justify-center">
      <Spinner size={28} label="Loading…" />
    </div>
  );
}
