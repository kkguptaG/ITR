// ---------------------------------------------------------------------------
// features/filing/download.ts
// Authenticated blob download for the PDF take-aways (ITR-V acknowledgment,
// computation worksheet, GST invoice). These endpoints stream application/pdf
// behind JWT auth, so a plain <a href> won't carry the bearer token — we fetch
// the blob through the axios instance and trigger a client-side save.
// ---------------------------------------------------------------------------

import { api } from '@/lib/api';

interface BlobResponse {
  data: Blob;
  headers: Record<string, string>;
}

/** Save a streamed PDF blob, preferring the server's Content-Disposition filename. */
function saveBlob(res: BlobResponse, suggestedName: string): void {
  const blob = res.data;
  const disposition = res.headers['content-disposition'] ?? '';
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

async function downloadPdf(url: string, suggestedName: string): Promise<void> {
  const res = await api.get(url, { responseType: 'blob' });
  saveBlob({ data: res.data as Blob, headers: res.headers as Record<string, string> }, suggestedName);
}

async function downloadPdfPost(url: string, body: unknown, suggestedName: string): Promise<void> {
  const res = await api.post(url, body, { responseType: 'blob' });
  saveBlob({ data: res.data as Blob, headers: res.headers as Record<string, string> }, suggestedName);
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

/** POST /tax/form-10e — the filled Form 10E (s.89 arrears relief) PDF for the signed-in user. */
export interface Form10ERequest {
  currentYearTotalIncome: number;
  arrears: { financialYear: string; totalIncomeOfThatYear: number; arrearsForThatYear: number }[];
}
export const downloadForm10E = (body: Form10ERequest) =>
  downloadPdfPost('/tax/form-10e', body, 'Form10E.pdf');
