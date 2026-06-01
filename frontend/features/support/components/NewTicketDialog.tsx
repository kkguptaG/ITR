'use client';

// NewTicketDialog — open a support ticket. Subject + category + priority + an
// optional first message. POST /tickets → routes to the new thread on success.

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslations } from 'next-intl';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Modal, Button, Field, Select, Input, Textarea, Alert } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { createTicket, supportKeys } from '../api';
import {
  TICKET_CATEGORIES,
  TICKET_PRIORITIES,
  toOptions,
} from '../helpers';

const schema = z.object({
  subject: z.string().trim().min(3).max(160),
  category: z.string().min(1),
  priority: z.string().min(1),
  message: z.string().trim().max(4000).optional().or(z.literal('')),
});

type FormValues = z.infer<typeof schema>;

export interface NewTicketDialogProps {
  open: boolean;
  onClose: () => void;
}

export function NewTicketDialog({ open, onClose }: NewTicketDialogProps) {
  const t = useTranslations('support');
  const tc = useTranslations('common');
  const router = useRouter();
  const queryClient = useQueryClient();

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { subject: '', category: 'General', priority: 'Normal', message: '' },
  });

  const mutation = useMutation({
    mutationFn: (values: FormValues) =>
      createTicket({
        subject: values.subject.trim(),
        category: values.category,
        priority: values.priority,
        message: values.message?.trim() || null,
      }),
    onSuccess: (ticket) => {
      void queryClient.invalidateQueries({ queryKey: supportKeys.tickets });
      onClose();
      router.push(`/support/tickets/${ticket.id}`);
    },
  });

  useEffect(() => {
    if (!open) {
      mutation.reset();
      reset({ subject: '', category: 'General', priority: 'Normal', message: '' });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const onSubmit = handleSubmit((values) => mutation.mutate(values));

  const errorMessage =
    mutation.error instanceof ApiError
      ? (mutation.error.firstFieldError ?? mutation.error.message)
      : null;

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={t('newTicketTitle')}
      description={t('newTicketSubtitle')}
      size="md"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={mutation.isPending}>
            {tc('cancel')}
          </Button>
          <Button onClick={onSubmit} loading={mutation.isPending}>
            {t('createTicket')}
          </Button>
        </>
      }
    >
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {errorMessage && <Alert variant="error">{errorMessage}</Alert>}

        <Field
          label={t('subject')}
          htmlFor="ticket-subject"
          required
          error={errors.subject ? t('subjectError') : null}
        >
          <Input
            id="ticket-subject"
            placeholder={t('subjectPlaceholder')}
            invalid={!!errors.subject}
            {...register('subject')}
          />
        </Field>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t('category')} htmlFor="ticket-category">
            <Select
              id="ticket-category"
              options={toOptions(TICKET_CATEGORIES)}
              {...register('category')}
            />
          </Field>
          <Field label={t('priority')} htmlFor="ticket-priority">
            <Select
              id="ticket-priority"
              options={toOptions(TICKET_PRIORITIES)}
              {...register('priority')}
            />
          </Field>
        </div>

        <Field label={t('messageOptional')} htmlFor="ticket-message" hint={t('messageHint')}>
          <Textarea
            id="ticket-message"
            rows={4}
            placeholder={t('messagePlaceholder')}
            {...register('message')}
          />
        </Field>
      </form>
    </Modal>
  );
}
