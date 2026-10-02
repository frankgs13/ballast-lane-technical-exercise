import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup } from '@angular/forms';

import { applyServerErrors, normalizeFieldErrors, toApiError } from './api-error';

describe('api-error', () => {
  it('reads the detail and field errors of a problem details response', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { title: 'One or more validation errors occurred.', errors: { title: ['Title is required.'] } },
    });

    const result = toApiError(error);

    expect(result.status).toBe(400);
    expect(result.message).toBe('One or more validation errors occurred.');
    expect(result.fieldErrors).toEqual({ title: ['Title is required.'] });
  });

  it('prefers detail over title', () => {
    const error = new HttpErrorResponse({
      status: 401,
      error: { title: 'Authentication failed.', detail: 'Invalid username or password.' },
    });

    expect(toApiError(error).message).toBe('Invalid username or password.');
  });

  it('explains network failures', () => {
    expect(toApiError(new HttpErrorResponse({ status: 0 })).message).toContain('Cannot reach the server');
  });

  it('explains rate limiting', () => {
    expect(toApiError(new HttpErrorResponse({ status: 429 })).message).toContain('Too many attempts');
  });

  it('handles non-HTTP errors', () => {
    expect(toApiError(new Error('boom')).status).toBe(-1);
  });

  it('normalizes ASP.NET model binding keys', () => {
    expect(normalizeFieldErrors({ '$.dueDate': ['Invalid date'], Title: ['Required'] })).toEqual({
      dueDate: ['Invalid date'],
      title: ['Required'],
    });
  });

  it('applies server errors to matching controls and returns the rest', () => {
    const form = new FormGroup({ title: new FormControl('') });

    const unmatched = applyServerErrors(form, { title: ['Too long.'], other: ['Unknown field.'] });

    expect(form.controls.title.getError('server')).toBe('Too long.');
    expect(form.controls.title.touched).toBe(true);
    expect(unmatched).toEqual(['Unknown field.']);
  });
});
