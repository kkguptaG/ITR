// ---------------------------------------------------------------------------
// features/shared — cross-cutting presentational components reused across the
// dashboard, filing wizard, return detail and CA workspace. All are
// self-contained: they take plain props (only lib/api-types enums + lib/format)
// so importing them never creates a feature-to-feature folder dependency.
//
// Import as: import { TaxSummaryPanel, RegimeCompareCard } from '@/features/shared';
// ---------------------------------------------------------------------------

export { StatusTimeline, type StatusTimelineProps } from './StatusTimeline';
export { RefundTrackerCard, type RefundTrackerCardProps } from './RefundTrackerCard';
export { TaxSummaryPanel, type TaxSummaryPanelProps } from './TaxSummaryPanel';
export { RegimeCompareCard, type RegimeCompareCardProps } from './RegimeCompareCard';
export {
  DeductionSuggestionCard,
  type DeductionSuggestionCardProps,
} from './DeductionSuggestionCard';
export type { ComputationView, DeductionSuggestionView } from './types';
