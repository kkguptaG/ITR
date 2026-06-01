// ---------------------------------------------------------------------------
// features/support/api.ts
// TanStack Query keys + thin fetchers for Support: tickets, notices vault, and
// the notification inbox. All calls go through lib/api (bearer + refresh-on-401
// + RFC7807 → ApiError). Endpoints per docs 04 §4.2.
// ---------------------------------------------------------------------------

import { apiGet, apiPost, apiPatch } from '@/lib/api';
import type { PagedResult } from '@/lib/api-types';
import type {
  CreateNoticeRequest,
  CreateNoticeResponseRequest,
  CreateTicketRequest,
  MarkNotificationsReadRequest,
  MarkNotificationsReadResponse,
  NoticeDetailDto,
  NoticeDto,
  NoticeResponseDto,
  NotificationDto,
  PostTicketMessageRequest,
  TicketDetailDto,
  TicketDto,
  TicketMessageDto,
  UnreadCountResponse,
  UpdateNoticeStatusRequest,
  UpdateTicketStatusRequest,
} from './types';

export interface ListParams {
  page?: number;
  pageSize?: number;
  status?: string;
}

/** Centralised query keys across the three support areas. */
export const supportKeys = {
  all: ['support'] as const,
  // tickets
  tickets: ['support', 'tickets'] as const,
  ticketList: (params: ListParams) => [...supportKeys.tickets, 'list', params] as const,
  ticket: (id: string) => [...supportKeys.tickets, 'detail', id] as const,
  // notices
  notices: ['support', 'notices'] as const,
  noticeList: (params: ListParams) => [...supportKeys.notices, 'list', params] as const,
  notice: (id: string) => [...supportKeys.notices, 'detail', id] as const,
  // notifications
  notifications: ['support', 'notifications'] as const,
  notificationList: (params: ListParams & { unreadOnly?: boolean }) =>
    [...supportKeys.notifications, 'list', params] as const,
  unreadCount: ['support', 'notifications', 'unread-count'] as const,
};

// ---- Tickets -------------------------------------------------------------
export function listTickets(params: ListParams): Promise<PagedResult<TicketDto>> {
  return apiGet<PagedResult<TicketDto>>('/tickets', { params });
}

export function getTicket(id: string): Promise<TicketDetailDto> {
  return apiGet<TicketDetailDto>(`/tickets/${id}`);
}

export function createTicket(body: CreateTicketRequest): Promise<TicketDetailDto> {
  return apiPost<TicketDetailDto>('/tickets', body);
}

export function postTicketMessage(
  id: string,
  body: PostTicketMessageRequest,
): Promise<TicketMessageDto> {
  return apiPost<TicketMessageDto>(`/tickets/${id}/messages`, body);
}

export function updateTicketStatus(
  id: string,
  body: UpdateTicketStatusRequest,
): Promise<TicketDto> {
  return apiPatch<TicketDto>(`/tickets/${id}:status`, body);
}

// ---- Notices vault -------------------------------------------------------
export function listNotices(params: ListParams): Promise<PagedResult<NoticeDto>> {
  return apiGet<PagedResult<NoticeDto>>('/notices', { params });
}

export function getNotice(id: string): Promise<NoticeDetailDto> {
  return apiGet<NoticeDetailDto>(`/notices/${id}`);
}

export function createNotice(body: CreateNoticeRequest): Promise<NoticeDetailDto> {
  return apiPost<NoticeDetailDto>('/notices', body);
}

export function addNoticeResponse(
  id: string,
  body: CreateNoticeResponseRequest,
): Promise<NoticeResponseDto> {
  return apiPost<NoticeResponseDto>(`/notices/${id}/responses`, body);
}

export function updateNoticeStatus(
  id: string,
  body: UpdateNoticeStatusRequest,
): Promise<NoticeDto> {
  return apiPatch<NoticeDto>(`/notices/${id}:status`, body);
}

// ---- Notifications -------------------------------------------------------
export function listNotifications(
  params: ListParams & { unreadOnly?: boolean },
): Promise<PagedResult<NotificationDto>> {
  return apiGet<PagedResult<NotificationDto>>('/notifications', { params });
}

export function getUnreadCount(): Promise<UnreadCountResponse> {
  return apiGet<UnreadCountResponse>('/notifications/unread-count');
}

export function markNotificationsRead(
  body: MarkNotificationsReadRequest,
): Promise<MarkNotificationsReadResponse> {
  return apiPost<MarkNotificationsReadResponse>('/notifications:mark-read', body);
}
