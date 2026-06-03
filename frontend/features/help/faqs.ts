// ---------------------------------------------------------------------------
// features/help/faqs.ts
// FAQ catalog for the Help Centre. Each entry references an i18n key pair under
// messages.help.faq.<id>.{q,a} so copy stays translatable (EN/HI). Grouped by
// topic; the page renders accessible disclosure rows.
// ---------------------------------------------------------------------------

export interface FaqGroup {
  /** i18n key under help.group.* */
  titleKey: string;
  /** FAQ ids; each maps to help.faq.<id>.q / .a */
  ids: string[];
}

export const faqGroups: FaqGroup[] = [
  {
    titleKey: 'filing',
    ids: ['whichItr', 'regime', 'documentsNeeded', 'editAfterFile'],
  },
  {
    titleKey: 'income',
    ids: ['cryptoVda', 'salaryArrears', 'advanceTax', 'aisReconciliation'],
  },
  {
    titleKey: 'documents',
    ids: ['form16', 'uploadSafe', 'extractionWrong'],
  },
  {
    titleKey: 'payments',
    ids: ['whenPay', 'refundFee', 'invoice'],
  },
  {
    titleKey: 'security',
    ids: ['dataStored', 'panSafe', 'deleteAccount'],
  },
];

/** Flat list of all FAQ ids (handy for search/indexing). */
export const allFaqIds = faqGroups.flatMap((g) => g.ids);
