// ---------------------------------------------------------------------------
// /returns/[returnId] — read-only return detail / status page.
// The wizard (…/file/[step]) owns editing; this is the destination after a
// return is filed, and the "View return" target from the returns list.
// ---------------------------------------------------------------------------

import { ReturnDetailView } from '@/features/filing/ReturnDetailView';

export default function ReturnDetailPage({ params }: { params: { returnId: string } }) {
  return <ReturnDetailView returnId={params.returnId} />;
}
