'use client';

// TicketThread — the full ticket conversation: header (subject, status, status
// transition control), the message bubbles, and a composer to post a reply.
// POST /tickets/{id}/messages appends; PATCH /tickets/{id}:status transitions.
// Own messages align right; agent/system messages align left.

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Send, Headphones } from 'lucide-react';
import {
  Card,
  CardContent,
  Badge,
  Button,
  Textarea,
  Select,
  Spinner,
  Alert,
} from '@/components/ui';
import { cn } from '@/lib/utils';
import { ApiError } from '@/lib/api';
import { useAuth } from '@/lib/auth';
import { formatDateTime } from '@/lib/format';
import {
  getTicket,
  postTicketMessage,
  updateTicketStatus,
  supportKeys,
} from '../api';
import type { TicketStatus } from '../types';
import { TICKET_STATUSES, ticketStatusTone } from '../helpers';

export function TicketThread({ ticketId }: { ticketId: string }) {
  const t = useTranslations('support');
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [body, setBody] = useState('');

  const query = useQuery({
    queryKey: supportKeys.ticket(ticketId),
    queryFn: () => getTicket(ticketId),
  });

  const postMessage = useMutation({
    mutationFn: () => postTicketMessage(ticketId, { body: body.trim() }),
    onSuccess: () => {
      setBody('');
      queryClient.invalidateQueries({ queryKey: supportKeys.ticket(ticketId) });
      queryClient.invalidateQueries({ queryKey: supportKeys.tickets });
    },
  });

  const changeStatus = useMutation({
    mutationFn: (status: TicketStatus) => updateTicketStatus(ticketId, { status }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: supportKeys.ticket(ticketId) });
      queryClient.invalidateQueries({ queryKey: supportKeys.tickets });
    },
  });

  if (query.isLoading) {
    return (
      <div className="flex justify-center py-16">
        <Spinner />
      </div>
    );
  }
  if (query.isError) {
    return (
      <Alert variant="error" title={t('ticketLoadError')}>
        {(query.error as ApiError).message}
      </Alert>
    );
  }
  if (!query.data) return null;

  const ticket = query.data;
  const isClosed = ticket.status === 'Closed';

  function submit(e: React.FormEvent) {
    e.preventDefault();
    if (body.trim().length === 0 || postMessage.isPending) return;
    postMessage.mutate();
  }

  return (
    <div className="space-y-5">
      {/* Header */}
      <Card>
        <CardContent className="flex flex-col gap-3 p-5 sm:flex-row sm:items-center sm:justify-between">
          <div className="min-w-0">
            <h1 className="truncate text-xl font-semibold text-ink-900">{ticket.subject}</h1>
            <div className="mt-1.5 flex flex-wrap items-center gap-2">
              <Badge tone={ticketStatusTone[ticket.status] ?? 'neutral'}>{ticket.status}</Badge>
              {ticket.category && <Badge tone="neutral">{ticket.category}</Badge>}
              <span className="text-xs text-ink-400">
                {t('openedOn', { date: formatDateTime(ticket.createdAt) })}
              </span>
            </div>
          </div>
          <div className="w-full sm:w-44">
            <Select
              aria-label={t('changeStatus')}
              value={ticket.status}
              disabled={changeStatus.isPending}
              onChange={(e) => changeStatus.mutate(e.target.value as TicketStatus)}
              options={TICKET_STATUSES.map((s) => ({
                value: s,
                label: t(`ticketStatus.${s}` as 'ticketStatus.Open'),
              }))}
            />
          </div>
        </CardContent>
      </Card>

      {changeStatus.isError && (
        <Alert variant="error" title={t('statusUpdateFailed')}>
          {(changeStatus.error as ApiError).message}
        </Alert>
      )}

      {/* Thread */}
      <Card>
        <CardContent className="space-y-4 p-5">
          {ticket.messages.length === 0 ? (
            <p className="py-6 text-center text-sm text-ink-500">{t('noMessages')}</p>
          ) : (
            ticket.messages.map((m) => {
              const isOwn = !!user && m.senderUserId === user.id && m.senderType === 'User';
              const isSystem = m.senderType === 'System';
              return (
                <div
                  key={m.id}
                  className={cn('flex', isOwn ? 'justify-end' : 'justify-start')}
                >
                  <div className={cn('max-w-[80%]', isOwn && 'text-right')}>
                    {!isOwn && (
                      <div className="mb-1 flex items-center gap-1.5 text-xs font-medium text-ink-500">
                        {!isSystem && <Headphones className="h-3.5 w-3.5" aria-hidden="true" />}
                        {isSystem ? t('senderSystem') : t('senderAgent')}
                      </div>
                    )}
                    <div
                      className={cn(
                        'inline-block rounded-2xl px-4 py-2.5 text-sm',
                        isOwn
                          ? 'bg-brand-600 text-white'
                          : isSystem
                            ? 'bg-ink-50 text-ink-600'
                            : 'bg-ink-100 text-ink-900',
                      )}
                    >
                      <p className="whitespace-pre-wrap text-left">{m.body}</p>
                    </div>
                    <p className="mt-1 text-xs text-ink-400">{formatDateTime(m.createdAt)}</p>
                  </div>
                </div>
              );
            })
          )}
        </CardContent>
      </Card>

      {/* Composer */}
      {isClosed ? (
        <Alert variant="info" title={t('ticketClosedTitle')}>
          {t('ticketClosedBody')}
        </Alert>
      ) : (
        <Card>
          <CardContent className="p-5">
            {postMessage.isError && (
              <Alert variant="error" className="mb-3">
                {(postMessage.error as ApiError).message}
              </Alert>
            )}
            <form onSubmit={submit} className="space-y-3">
              <Textarea
                value={body}
                onChange={(e) => setBody(e.target.value)}
                placeholder={t('replyPlaceholder')}
                rows={3}
                aria-label={t('replyLabel')}
                disabled={postMessage.isPending}
              />
              <div className="flex justify-end">
                <Button
                  type="submit"
                  loading={postMessage.isPending}
                  disabled={body.trim().length === 0}
                >
                  <Send className="h-4 w-4" aria-hidden="true" />
                  {t('sendReply')}
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
