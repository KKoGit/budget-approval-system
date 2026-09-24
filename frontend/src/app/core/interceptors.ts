import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { ApiError } from './models';
import { NotificationService } from './notification.service';
import { SessionService } from './session.service';

/**
 * Attaches the demo identity to API calls. Swapping to real authentication means replacing this one
 * interceptor with one that adds `Authorization: Bearer <token>`.
 */
export const demoAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const user = inject(SessionService).user();
  if (!user || !req.url.startsWith('/api')) return next(req);
  return next(req.clone({ setHeaders: { 'X-Demo-User': String(user.id) } }));
};

/**
 * Converts every HTTP failure into an {@link ApiError} carrying the API's stable error code, and handles the
 * cross-cutting cases centrally: expired session (401), no connection (0) and server faults (5xx).
 * Business-rule (422), conflict (409) and validation (400) errors are left to the screen that caused them,
 * because only that screen knows where to show the message.
 */
export const apiErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);
  const session = inject(SessionService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((response: HttpErrorResponse) => {
      const error = toApiError(response);

      if (response.status === 401) {
        session.signOut();
        void router.navigate(['/sign-in']);
        notifications.info('Choose a user to continue.');
      } else if (response.status === 0) {
        notifications.error('Cannot reach the API. Check that it is running on http://localhost:5080.');
      } else if (response.status >= 500) {
        notifications.error(error.detail);
      } else if (response.status === 403) {
        notifications.error(error.detail);
      }

      return throwError(() => error);
    })
  );
};

function toApiError(response: HttpErrorResponse): ApiError {
  const body = (response.error ?? {}) as { code?: string; title?: string; detail?: string; errors?: Record<string, string[]> };
  const fieldErrors = body.errors ?? {};
  const firstFieldError = Object.values(fieldErrors)[0]?.[0];

  return new ApiError(
    response.status,
    body.code ?? (response.status === 400 ? 'VALIDATION' : `HTTP_${response.status}`),
    body.title ?? response.statusText,
    body.detail ?? firstFieldError ?? 'The request could not be completed.',
    fieldErrors
  );
}
