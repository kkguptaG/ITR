// ---------------------------------------------------------------------------
// utils.ts — small cross-cutting helpers.
// ---------------------------------------------------------------------------
import { clsx, type ClassValue } from 'clsx';

/** Conditional className join (clsx). We avoid tailwind-merge to keep deps minimal;
 *  components should not pass conflicting Tailwind utilities for the same property. */
export function cn(...inputs: ClassValue[]): string {
  return clsx(inputs);
}

/** Stable id generator for label/control associations when one isn't provided. */
let idCounter = 0;
export function genId(prefix = 'tg'): string {
  idCounter += 1;
  return `${prefix}-${idCounter}`;
}
