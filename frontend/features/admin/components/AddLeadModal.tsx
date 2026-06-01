'use client';

// Add-lead modal for the CRM pipeline. Creates a lead via POST /admin/leads and
// invalidates the pipeline + lead lists so the new card appears immediately.

import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Modal, Button, Field, Input } from '@/components/ui';
import { Alert } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { adminKeys, createLead } from '@/features/admin';

const schema = z
  .object({
    name: z.string().min(2, 'Enter a name (min 2 characters).'),
    email: z.string().email('Enter a valid email.').or(z.literal('')),
    mobile: z
      .string()
      .regex(/^[0-9+\-\s]{6,15}$/, 'Enter a valid mobile number.')
      .or(z.literal('')),
    source: z.string().max(60).optional(),
  })
  .refine((v) => v.email !== '' || v.mobile !== '', {
    message: 'Provide at least an email or a mobile number.',
    path: ['email'],
  });

type FormValues = z.infer<typeof schema>;

export function AddLeadModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const qc = useQueryClient();

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', email: '', mobile: '', source: '' },
  });

  const mutation = useMutation({
    mutationFn: (values: FormValues) =>
      createLead({
        name: values.name,
        email: values.email || null,
        mobile: values.mobile || null,
        source: values.source || 'manual',
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: adminKeys.leads() });
      onClose();
    },
  });

  useEffect(() => {
    if (!open) {
      mutation.reset();
      reset({ name: '', email: '', mobile: '', source: '' });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const onSubmit = handleSubmit((values) => mutation.mutate(values));
  const error = (mutation.error as ApiError | undefined)?.message;

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="md"
      title="Add lead"
      description="Capture a new prospect into the pipeline."
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={mutation.isPending}>
            Cancel
          </Button>
          <Button onClick={onSubmit} loading={mutation.isPending}>
            Create lead
          </Button>
        </>
      }
    >
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {error && <Alert variant="error">{error}</Alert>}

        <Field label="Name" htmlFor="lead-name" required error={errors.name?.message}>
          <Input id="lead-name" placeholder="e.g. Rahul Sharma" {...register('name')} />
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Email" htmlFor="lead-email" error={errors.email?.message}>
            <Input id="lead-email" type="email" placeholder="name@example.com" {...register('email')} />
          </Field>
          <Field label="Mobile" htmlFor="lead-mobile" error={errors.mobile?.message}>
            <Input id="lead-mobile" placeholder="+91…" {...register('mobile')} />
          </Field>
        </div>
        <Field label="Source" htmlFor="lead-source" hint="Where did this lead come from?">
          <Input id="lead-source" placeholder="e.g. website, referral, ad" {...register('source')} />
        </Field>
      </form>
    </Modal>
  );
}
