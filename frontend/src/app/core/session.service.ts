import { Injectable, computed, signal } from '@angular/core';
import { Role, UserDto } from './models';

const STORAGE_KEY = 'budget-approval.demo-user';

/**
 * Holds the signed-in demo persona. In production this is replaced by an OIDC client (e.g. MSAL) and the
 * interceptor attaches a bearer token instead of the X-Demo-User header; nothing else in the app changes.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly _user = signal<UserDto | null>(this.restore());

  readonly user = this._user.asReadonly();
  readonly isSignedIn = computed(() => this._user() !== null);
  readonly isApprover = computed(() => this.hasRole('Approver'));
  readonly isRequester = computed(() => this.hasRole('Requester'));

  signIn(user: UserDto): void {
    this._user.set(user);
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(user)); } catch { /* storage unavailable: session-only */ }
  }

  signOut(): void {
    this._user.set(null);
    try { localStorage.removeItem(STORAGE_KEY); } catch { /* ignore */ }
  }

  private hasRole(role: Role): boolean {
    return this._user()?.roles.includes(role) ?? false;
  }

  private restore(): UserDto | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as UserDto) : null;
    } catch {
      return null;
    }
  }
}
