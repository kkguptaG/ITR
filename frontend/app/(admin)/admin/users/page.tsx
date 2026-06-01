'use client';

// ---------------------------------------------------------------------------
// /admin/users — searchable, paged user board.
//   • Debounced search across name/email/mobile (GET /admin/users?search=)
//   • Row click opens UserDetailModal: change status, assign/remove roles
// Restricted (UI) to Admin/SuperAdmin; the API enforces the real RBAC.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Search, Users as UsersIcon } from 'lucide-react';
import {
  Input,
  Table,
  THead,
  TBody,
  TR,
  TH,
  TD,
  Spinner,
  Alert,
  EmptyState,
} from '@/components/ui';
import { formatDate, formatRelative } from '@/lib/format';
import {
  PageHeader,
  Pagination,
  UserStatusBadge,
  RoleChips,
  useDebouncedValue,
  adminKeys,
  listUsers,
  type AdminUserListItemDto,
} from '@/features/admin';
import { UserDetailModal } from '@/features/admin/components/UserDetailModal';

const PAGE_SIZE = 15;

export default function AdminUsersPage() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<AdminUserListItemDto | null>(null);

  const debouncedSearch = useDebouncedValue(search.trim(), 350);
  const params = {
    page,
    pageSize: PAGE_SIZE,
    search: debouncedSearch || undefined,
  };

  const query = useQuery({
    queryKey: adminKeys.userList(params),
    queryFn: () => listUsers(params),
    placeholderData: keepPreviousData,
  });

  const items = query.data?.items ?? [];
  const total = query.data?.total ?? 0;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Users"
        subtitle="Search accounts, manage status and assign roles."
      />

      {/* Search bar */}
      <div className="relative max-w-md">
        <Search
          className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-400"
          aria-hidden="true"
        />
        <Input
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
          placeholder="Search by name, email or mobile…"
          className="pl-9"
          aria-label="Search users"
        />
      </div>

      {query.isLoading ? (
        <div className="flex min-h-[30vh] items-center justify-center">
          <Spinner size={28} label="Loading users…" />
        </div>
      ) : query.isError ? (
        <Alert variant="error">We couldn’t load users. Please try again.</Alert>
      ) : items.length === 0 ? (
        <EmptyState
          icon={UsersIcon}
          title={debouncedSearch ? 'No matching users' : 'No users yet'}
          description={
            debouncedSearch
              ? 'Try a different name, email or mobile number.'
              : 'Users will appear here as they register.'
          }
        />
      ) : (
        <>
          <div className="flex items-center justify-between text-sm text-ink-500">
            <span>
              {total} {total === 1 ? 'user' : 'users'}
            </span>
            {query.isFetching && <Spinner label="Updating…" />}
          </div>

          <Table>
            <THead>
              <TR className="hover:bg-transparent">
                <TH>Name</TH>
                <TH>Contact</TH>
                <TH>Roles</TH>
                <TH>Status</TH>
                <TH>Last login</TH>
                <TH>Joined</TH>
              </TR>
            </THead>
            <TBody>
              {items.map((u) => (
                <TR
                  key={u.id}
                  className="cursor-pointer"
                  onClick={() => setSelected(u)}
                  tabIndex={0}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      setSelected(u);
                    }
                  }}
                >
                  <TD className="font-medium text-ink-900">
                    {u.fullName}
                    {u.panMasked && (
                      <span className="ml-2 text-xs font-normal text-ink-400">{u.panMasked}</span>
                    )}
                  </TD>
                  <TD className="text-ink-600">
                    <div className="flex flex-col">
                      <span className="truncate">{u.email ?? '—'}</span>
                      <span className="text-xs text-ink-400">{u.mobile ?? '—'}</span>
                    </div>
                  </TD>
                  <TD>
                    <RoleChips roles={u.roles} />
                  </TD>
                  <TD>
                    <UserStatusBadge status={u.status} />
                  </TD>
                  <TD className="whitespace-nowrap text-ink-500">
                    {u.lastLoginAt ? formatRelative(u.lastLoginAt) : 'Never'}
                  </TD>
                  <TD className="whitespace-nowrap text-ink-500">{formatDate(u.createdAt)}</TD>
                </TR>
              ))}
            </TBody>
          </Table>

          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            total={total}
            isFetching={query.isFetching}
            onPageChange={setPage}
          />
        </>
      )}

      <UserDetailModal
        user={selected}
        open={selected !== null}
        onClose={() => setSelected(null)}
      />
    </div>
  );
}
