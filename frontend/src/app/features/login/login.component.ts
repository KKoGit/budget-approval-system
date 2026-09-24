import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { BudgetApiService } from '../../core/budget-api.service';
import { UserDto } from '../../core/models';
import { SessionService } from '../../core/session.service';

/** What each seeded persona is useful for when reviewing the demo. Keyed by e-mail so ids can change. */
const WALKTHROUGH: Record<string, string> = {
  'maya.chen@asencilla.example': 'Create a request, save it as a draft, then submit it.',
  'luis.ortega@asencilla.example': 'Owns the roof repair that is larger than Facilities has left this year.',
  'priya.natarajan@asencilla.example': 'Has a request returned for revision. Edit and resubmit it.',
  'samuel.okafor@asencilla.example': 'Two requests waiting for a decision.',
  'dana.whitfield@asencilla.example': 'Approver who also raises requests. Try approving her own survey platform request.',
  'marcus.bell@asencilla.example': 'Budget Office approver. Work through the approval queue.'
};

@Component({
  selector: 'app-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sign-in">
      <header class="sign-in__head">
        <span class="rail__seal" aria-hidden="true">AS</span>
        <div>
          <h1>Budget requests</h1>
          <p>Ascencilla Regional Services Agency. This demo uses invented people and figures.</p>
        </div>
      </header>

      <h2 class="sign-in__prompt">Continue as</h2>
      @if (error()) {
        <div class="callout callout--error" role="alert">
          {{ error() }}
          <button type="button" class="link-button" (click)="load()">Try again</button>
        </div>
      }
      <ul class="personas">
        @for (user of users(); track user.id) {
          <li>
            <button type="button" class="persona" (click)="choose(user)">
              <span class="persona__name">{{ user.displayName }}</span>
              <span class="persona__meta">{{ user.departmentName }}, {{ user.roles.join(' and ').toLowerCase() }}</span>
              <span class="persona__hint">{{ hint(user) }}</span>
            </button>
          </li>
        } @empty {
          @if (!error()) { <li class="muted">Loading people…</li> }
        }
      </ul>
      <p class="muted small">Sign-in is simulated for review. Production uses the agency's identity provider; see docs/security.md.</p>
    </div>
  `
})
export class LoginComponent {
  readonly returnUrl = input<string>();

  private readonly api = inject(BudgetApiService);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  protected readonly users = signal<UserDto[]>([]);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.error.set(null);
    this.api.demoUsers().subscribe({
      next: users => this.users.set(users),
      error: () => this.error.set(environment.apiUnavailableHelp)
    });
  }

  protected hint(user: UserDto): string {
    return WALKTHROUGH[user.email] ?? '';
  }

  protected choose(user: UserDto): void {
    this.session.signIn(user);
    this.api.clearCache();
    void this.router.navigateByUrl(this.returnUrl() || '/dashboard');
  }
}
