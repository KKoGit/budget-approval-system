import { inject } from '@angular/core';
import { CanActivateFn, CanDeactivateFn, Router } from '@angular/router';
import { NotificationService } from './notification.service';
import { SessionService } from './session.service';

/** Every screen except sign-in needs a persona. */
export const authGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionService);
  return session.isSignedIn()
    ? true
    : inject(Router).createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } });
};

/** UX guard only: the API enforces the Approver policy independently. */
export const approverGuard: CanActivateFn = () => {
  if (inject(SessionService).isApprover()) return true;
  inject(NotificationService).info('The approval queue is only available to approvers.');
  return inject(Router).createUrlTree(['/dashboard']);
};

export const requesterGuard: CanActivateFn = () => {
  if (inject(SessionService).isRequester()) return true;
  inject(NotificationService).info('Only requesters can create budget requests.');
  return inject(Router).createUrlTree(['/requests']);
};

export interface HasUnsavedChanges { hasUnsavedChanges(): boolean; }

export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = component =>
  !component.hasUnsavedChanges() || confirm('You have unsaved changes. Leave this page and discard them?');
