'use client';

// ---------------------------------------------------------------------------
// EditableList — a small CRUD list scaffold reused by every income head:
//   • renders existing rows (summary + edit/delete)
//   • an "add" affordance that reveals an inline form
//   • delete with an inline confirm
// The parent supplies the row renderer + the form renderer; this owns the
// open/close + delete-confirm UI state only. Mutations live in the parent.
// ---------------------------------------------------------------------------

import { useState, type ReactNode } from 'react';
import { useTranslations } from 'next-intl';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { Button, Card } from '@/components/ui';

export interface EditableListProps<T> {
  items: T[];
  getKey: (item: T) => string;
  renderSummary: (item: T) => ReactNode;
  /** Form for adding/editing; `item` is undefined when adding. `onDone` closes the form. */
  renderForm: (item: T | undefined, onDone: () => void) => ReactNode;
  onDelete?: (item: T) => void;
  deleting?: boolean;
  addLabel: string;
  emptyLabel: string;
  /** When true, hides the "add" button (e.g. ITR-1 single house property already present). */
  maxOneReached?: boolean;
}

export function EditableList<T>({
  items,
  getKey,
  renderSummary,
  renderForm,
  onDelete,
  deleting,
  addLabel,
  emptyLabel,
  maxOneReached,
}: EditableListProps<T>) {
  const tc = useTranslations('common');
  const [adding, setAdding] = useState(false);
  const [editingKey, setEditingKey] = useState<string | null>(null);
  const [confirmKey, setConfirmKey] = useState<string | null>(null);

  return (
    <div className="space-y-3">
      {items.length === 0 && !adding && (
        <p className="rounded-xl border border-dashed border-ink-300 px-4 py-6 text-center text-sm text-ink-500">
          {emptyLabel}
        </p>
      )}

      {items.map((item) => {
        const key = getKey(item);
        if (editingKey === key) {
          return (
            <Card key={key} className="p-4">
              {renderForm(item, () => setEditingKey(null))}
            </Card>
          );
        }
        return (
          <Card key={key} className="flex items-start justify-between gap-3 p-4">
            <div className="min-w-0 flex-1">{renderSummary(item)}</div>
            <div className="flex shrink-0 items-center gap-1">
              <Button variant="ghost" size="sm" onClick={() => setEditingKey(key)} aria-label={tc('edit')}>
                <Pencil className="h-4 w-4" aria-hidden="true" />
              </Button>
              {onDelete &&
                (confirmKey === key ? (
                  <div className="flex items-center gap-1">
                    <Button
                      variant="destructive"
                      size="sm"
                      loading={deleting}
                      onClick={() => {
                        onDelete(item);
                        setConfirmKey(null);
                      }}
                    >
                      {tc('delete')}
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => setConfirmKey(null)}>
                      {tc('cancel')}
                    </Button>
                  </div>
                ) : (
                  <Button variant="ghost" size="sm" onClick={() => setConfirmKey(key)} aria-label={tc('remove')}>
                    <Trash2 className="h-4 w-4 text-red-500" aria-hidden="true" />
                  </Button>
                ))}
            </div>
          </Card>
        );
      })}

      {adding ? (
        <Card className="p-4">{renderForm(undefined, () => setAdding(false))}</Card>
      ) : (
        !maxOneReached && (
          <Button variant="outline" onClick={() => setAdding(true)} className="w-full">
            <Plus className="h-4 w-4" aria-hidden="true" />
            {addLabel}
          </Button>
        )
      )}
    </div>
  );
}
