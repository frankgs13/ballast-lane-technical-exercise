import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { AuthService } from './auth.service';

/**
 * Attaches the bearer token to calls to our own API only (never leak it to third-party URLs)
 * and ends the session when the API rejects a token we believed was valid.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken;
  const isApiCall = request.url.startsWith('/api/');

  const outgoing =
    token && isApiCall ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

  return next(outgoing).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && token && isApiCall) {
        auth.logout({ reason: 'expired' });
      }
      return throwError(() => error);
    }),
  );
};
