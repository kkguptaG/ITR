// Barrel for the Documents feature.
// The FileDropzone + ExtractionReviewDrawer are part of the public surface so
// the filing wizard's "Documents" step can reuse the uploader:
// import { FileDropzone } from '@/features/documents'.
export { FileDropzone, type FileDropzoneProps } from './components/FileDropzone';
export { DocumentsTable, type DocumentsTableProps } from './components/DocumentsTable';
export {
  ExtractionReviewDrawer,
  type ExtractionReviewDrawerProps,
} from './components/ExtractionReviewDrawer';
export { Drawer, type DrawerProps } from './components/Drawer';
export * from './api';
export * from './helpers';
export type * from './types';
