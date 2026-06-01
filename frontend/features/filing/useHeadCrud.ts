'use client';

// ---------------------------------------------------------------------------
// useHeadCrud — generic list + add/update/delete wiring for one income head
// (salary, house property, capital gain, business, other source). Each mutation
// invalidates the head list AND the return detail (so the summary/computation
// reflect the change). Keeps the Income step declarative.
// ---------------------------------------------------------------------------

import { useMutation, useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query';
import { filingKeys } from './api';

export interface HeadApi<TDto, TReq> {
  list: (returnId: string) => Promise<TDto[]>;
  add: (returnId: string, body: TReq) => Promise<TDto>;
  update: (returnId: string, id: string, body: TReq) => Promise<TDto>;
  remove: (returnId: string, id: string) => Promise<void>;
}

export function useHeadCrud<TDto, TReq>(
  returnId: string,
  queryKey: QueryKey,
  api: HeadApi<TDto, TReq>,
  onAfterChange?: () => void,
) {
  const qc = useQueryClient();

  const invalidate = () => {
    void qc.invalidateQueries({ queryKey });
    void qc.invalidateQueries({ queryKey: filingKeys.detail(returnId) });
    onAfterChange?.();
  };

  const query = useQuery({
    queryKey,
    queryFn: () => api.list(returnId),
    staleTime: 5_000,
  });

  const addMutation = useMutation({
    mutationFn: (body: TReq) => api.add(returnId, body),
    onSuccess: invalidate,
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, body }: { id: string; body: TReq }) => api.update(returnId, id, body),
    onSuccess: invalidate,
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.remove(returnId, id),
    onSuccess: invalidate,
  });

  return { query, addMutation, updateMutation, deleteMutation };
}
