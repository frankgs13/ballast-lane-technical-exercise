import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormGroup } from '@angular/forms';

/** Normalized view of an RFC 7807 problem details response. */
export interface ApiError {
  status: number;
  message: string;
  fieldErrors: Record<string, string[]>;
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: -1, message: 'Something went wrong. Please try again.', fieldErrors: {} };
  }

  if (error.status === 0) {
    return { status: 0, message: 'Cannot reach the server. Is the API running?', fieldErrors: {} };
  }

  if (error.status === 429) {
    return { status: 429, message: 'Too many attempts. Please wait a minute and try again.', fieldErrors: {} };
  }

  const body = (typeof error.error === 'object' ? error.error : null) as ProblemDetails | null;
  return {
    status: error.status,
    message: body?.detail ?? body?.title ?? 'Something went wrong. Please try again.',
    fieldErrors: normalizeFieldErrors(body?.errors),
  };
}

/**
 * ASP.NET reports model-binding errors as "$.dueDate" and domain errors as "dueDate";
 * both are mapped onto the matching form control name.
 */
export function normalizeFieldErrors(errors: Record<string, string[]> | undefined): Record<string, string[]> {
  const result: Record<string, string[]> = {};
  for (const [key, messages] of Object.entries(errors ?? {})) {
    const cleaned = key.replace(/^\$\./, '');
    const field = cleaned.charAt(0).toLowerCase() + cleaned.slice(1);
    result[field] = [...(result[field] ?? []), ...messages];
  }
  return result;
}

/**
 * Shows server-side validation messages next to the matching inputs.
 * @returns messages that do not belong to any control, to be shown in a banner.
 */
export function applyServerErrors(form: FormGroup, fieldErrors: Record<string, string[]>): string[] {
  const unmatched: string[] = [];
  for (const [field, messages] of Object.entries(fieldErrors)) {
    const control: AbstractControl | null = form.get(field);
    if (control) {
      control.setErrors({ ...control.errors, server: messages.join(' ') });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }
  return unmatched;
}
