// ---------------------------------------------------------------------------
// features/support/helpers.ts
// Pure helpers for Support: status → badge tone maps, option lists, and a
// File → base64 reader for the notices vault inline-upload contract.
// ---------------------------------------------------------------------------

import type { SelectOption } from '@/components/ui';
import type { NoticeStatus, TicketStatus } from './types';

type Tone = 'neutral' | 'brand' | 'success' | 'warning' | 'danger' | 'info';

// ---- Tickets -------------------------------------------------------------
export const ticketStatusTone: Record<string, Tone> = {
  Open: 'info',
  Pending: 'warning',
  Resolved: 'success',
  Closed: 'neutral',
};

export const ticketPriorityTone: Record<string, Tone> = {
  Low: 'neutral',
  Normal: 'info',
  High: 'warning',
  Urgent: 'danger',
};

export const TICKET_STATUSES: TicketStatus[] = ['Open', 'Pending', 'Resolved', 'Closed'];
export const TICKET_PRIORITIES = ['Low', 'Normal', 'High', 'Urgent'] as const;
export const TICKET_CATEGORIES = [
  'General',
  'Filing',
  'Payment',
  'Documents',
  'Technical',
  'Other',
] as const;

// ---- Notices -------------------------------------------------------------
export const noticeStatusTone: Record<string, Tone> = {
  Open: 'warning',
  InProgress: 'info',
  Responded: 'brand',
  Closed: 'neutral',
  Escalated: 'danger',
};

export const NOTICE_STATUSES: NoticeStatus[] = [
  'Open',
  'InProgress',
  'Responded',
  'Closed',
  'Escalated',
];

/** Common ITD notice types (free-text on the backend; these seed the picker). */
export const NOTICE_TYPES = [
  '143(1) Intimation',
  '139(9) Defective Return',
  '142(1) Inquiry',
  '143(2) Scrutiny',
  '148 Reassessment',
  '245 Adjustment',
  'Other',
] as const;

// ---- Notifications -------------------------------------------------------
export const channelTone: Record<string, Tone> = {
  Email: 'info',
  Sms: 'brand',
  WhatsApp: 'success',
  InApp: 'neutral',
};

// ---- Shared --------------------------------------------------------------
/** Build SelectOptions from a readonly string list. */
export function toOptions(values: readonly string[]): SelectOption[] {
  return values.map((v) => ({ value: v, label: v }));
}

/**
 * Read a File as a base64 string (no data: prefix) for the notices inline-upload
 * contract (CreateNoticeRequest.fileBase64). Rejects on read error.
 */
export function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result;
      if (typeof result !== 'string') {
        reject(new Error('Unexpected file read result'));
        return;
      }
      // result is "data:<mime>;base64,<payload>" — strip the prefix.
      const comma = result.indexOf(',');
      resolve(comma >= 0 ? result.slice(comma + 1) : result);
    };
    reader.onerror = () => reject(reader.error ?? new Error('File read failed'));
    reader.readAsDataURL(file);
  });
}

/** Max inline upload size for the notices vault (keep base64 payloads sane). */
export const MAX_NOTICE_FILE_BYTES = 10 * 1024 * 1024; // 10 MB
