// ---------------------------------------------------------------------------
// features/filing/download.ts
// Authenticated blob download for the PDF take-aways (ITR-V acknowledgment,
// computation worksheet, GST invoice). These endpoints stream application/pdf
// behind JWT auth, so a plain <a href> won't carry the bearer token — we fetch
// the blob through the axios instance and trigger a client-side save.
// ---------------------------------------------------------------------------

import { api } from '@/lib/api';

async function downloadPdf(url: string, suggestedName: string): Promise<void> {
  const res = await api.get(url, { responseType: 'blob' });
  const blob = res.data as Blob;

  // Prefer the server-provided filename if present.
  const disposition = (res.headers as Record<string, string>)['content-disposition'] ?? '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  const fileName = match ? decodeURIComponent(match[1]) : suggestedName;

  const objectUrl = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = objectUrl;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  // Revoke on the next tick so the navigation has a chance to start.
  setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
}

/** GET /returns/{id}/acknowledgment — the ITR-V acknowledgment PDF. */
export const downloadAcknowledgment = (returnId: string) =>
  downloadPdf(`/returns/${returnId}/acknowledgment`, `ITR-V-${returnId}.pdf`);

/** GET /returns/{id}/computation — the computation worksheet PDF. */
export const downloadComputation = (returnId: string) =>
  downloadPdf(`/returns/${returnId}/computation`, `computation-${returnId}.pdf`);

/** GET /payments/{id}/invoice:pdf — the GST tax-invoice PDF. */
export const downloadInvoice = (paymentId: string) =>
  downloadPdf(`/payments/${paymentId}/invoice:pdf`, `invoice-${paymentId}.pdf`);
