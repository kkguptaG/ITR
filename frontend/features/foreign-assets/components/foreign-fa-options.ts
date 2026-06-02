// Shared dropdown options for the Schedule FA immovable / financial-interest cards.

export const OWNERSHIPS = [
  { value: 'DIRECT', label: 'Direct' },
  { value: 'BENEFICIAL_OWNER', label: 'Beneficial owner' },
  { value: 'BENIFICIARY', label: 'Beneficiary' },
];

// Which Indian schedule the foreign income was offered to tax in.
export const INCOME_SCHEDULES = [
  { value: 'SA', label: 'Salary' },
  { value: 'HP', label: 'House property' },
  { value: 'CG', label: 'Capital gains' },
  { value: 'OS', label: 'Other sources' },
  { value: 'EI', label: 'Exempt income' },
  { value: 'NI', label: 'Not taxable in India' },
];
