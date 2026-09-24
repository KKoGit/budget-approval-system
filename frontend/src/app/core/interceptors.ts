import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, finalize, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { API_ROOT } from './api-root';
import { ApiError } from './models';
import { NotificationService } from './notification.service';
import { SessionService } from './session.service';

/**
 * Attaches the demo identity to API calls. Swapping to real authentication means replacing this one
 * interceptor with one that adds `Authorization: Bearer <token>`.
 */
export const demoAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const user = inject(SessionService).user();
  if (!user || !req.url.startsWith(API_ROOT)) return next(req);
  return next(req.clone({ setHeaders: { 'X-Demo-User': String(user.id) } }));
};

let coldStartNoticeShown = false;

/**
 * The hosted demo API runs on a free plan that sleeps when idle, so the first request can take 20–30 seconds.
 * If a response is slow, say so once instead of leaving the page looking frozen. Off for local development.
 */
export const coldStartInterceptor: HttpInterceptorFn = (req, next) => {
  if (!environment.coldStartNotice || coldStartNoticeShown || !req.url.startsWith(API_ROOT)) return next(req);
  const notifications = inject(NotificationService);
  const timer = setTimeout(() => {
    if (coldStartNoticeShown) return;
    coldStartNoticeShown = true;
    notifications.info('Waking up the demo server. The first request after a quiet period can take up to 30 seconds.');
  }, 4000);
  return next(req).pipe(finalize(() => clearTimeout(timer)));
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
        notifications.error(`Cannot reach ${environment.apiDescription}. Check that it is running, then try again.`);
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
