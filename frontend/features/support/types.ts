// ---------------------------------------------------------------------------
// features/support/types.ts
// Wire types for Support: tickets, notices vault, and the notification inbox.
// Mirror the backend records (Api/Modules/Support/* and Api/Modules/Notices/*),
// which are richer than the generic ones in lib/api-types — so we keep
// feature-local types matching the actual /tickets, /notices, /notifications
// endpoints exactly. Source: docs/architecture/04-api-and-auth.md §4.2.
// ---------------------------------------------------------------------------

import type { Guid, IsoDateTime, NotificationChannel } from '@/lib/api-types';

// ---- Tickets -------------------------------------------------------------
/** Ticket status strings accepted by PATCH /tickets/{id}:status. */
export type TicketStatus = 'Open' | 'Pending' | 'Resolved' | 'Closed';

export interface TicketMessageDto {
  id: Guid;
  senderUserId: Guid;
  /** "User" | "Agent" | "System" — drives bubble alignment. */
  senderType: string;
  body: string;
  createdAt: IsoDateTime;
}

export interface TicketDto {
  id: Guid;
  subject: string;
  category?: string | null;
  status: string;
  priority: string;
  taxReturnId?: Guid | null;
  assignedAgentId?: Guid | null;
  createdAt: IsoDateTime;
  updatedAt: IsoDateTime;
}

export interface TicketDetailDto extends TicketDto {
  messages: TicketMessageDto[];
}

export interface CreateTicketRequest {
  subject: string;
  category?: string | null;
  priority?: string | null;
  taxReturnId?: Guid | null;
  message?: string | null;
}

export interface PostTicketMessageRequest {
  body: string;
}

export interface UpdateTicketStatusRequest {
  status: TicketStatus;
}

// ---- Notices vault -------------------------------------------------------
/** Notice status strings accepted by PATCH /notices/{id}:status. */
export type NoticeStatus = 'Open' | 'InProgress' | 'Responded' | 'Closed' | 'Escalated';

export interface NoticeResponseDto {
  id: Guid;
  responseText: string;
  responseType?: string | null;
  hasAttachment: boolean;
  respondedByUserId?: Guid | null;
  acknowledgementNo?: string | null;
  createdAt: IsoDateTime;
}

export interface NoticeDto {
  id: Guid;
  noticeType: string;
  section?: string | null;
  din?: string | null;
  taxReturnId?: Guid | null;
  receivedAt: IsoDateTime;
  /** "YYYY-MM-DD" (DateOnly on the wire). */
  dueDate?: string | null;
  summary?: string | null;
  demandAmount?: string | null;
  refundAmount?: string | null;
  status: string;
  hasAttachment: boolean;
  createdAt: IsoDateTime;
}

export interface NoticeDetailDto extends NoticeDto {
  responses: NoticeResponseDto[];
}

/**
 * POST /notices body. A scanned copy is uploaded inline as base64 (the V1
 * passive vault stores it via IFileStorage — not the two-step pre-signed flow
 * used for filing documents).
 */
export interface CreateNoticeRequest {
  noticeType: string;
  section?: string | null;
  din?: string | null;
  taxReturnId?: Guid | null;
  receivedAt?: IsoDateTime | null;
  dueDate?: string | null;
  summary?: string | null;
  demandAmount?: number | null;
  refundAmount?: number | null;
  fileName?: string | null;
  contentType?: string | null;
  fileBase64?: string | null;
}

export interface CreateNoticeResponseRequest {
  responseText: string;
  responseType?: string | null;
  fileName?: string | null;
  contentType?: string | null;
  fileBase64?: string | null;
}

export interface UpdateNoticeStatusRequest {
  status: NoticeStatus;
}

// ---- Notifications -------------------------------------------------------
export interface NotificationDto {
  id: Guid;
  channel: NotificationChannel | string;
  template: string;
  title?: string | null;
  body?: string | null;
  status: string;
  isRead: boolean;
  readAt?: IsoDateTime | null;
  sentAt?: IsoDateTime | null;
  createdAt: IsoDateTime;
}

export interface MarkNotificationsReadRequest {
  /** Null/empty marks every unread notification read. */
  ids?: Guid[] | null;
}

export interface MarkNotificationsReadResponse {
  markedRead: number;
}

export interface UnreadCountResponse {
  unread: number;
}
