import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BudgetApiService } from './core/budget-api.service';
import { SessionService } from './core/session.service';
import { ToastHostComponent } from './shared/toast-host.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastHostComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="skip-link" href="#main">Skip to content</a>
    @if (session.user(); as user) {
      <div class="shell">
        <nav class="rail" aria-label="Main">
          <div class="rail__brand">
            <span class="rail__seal" aria-hidden="true">AS</span>
            <span>ASencilla<br><small>Budget requests</small></span>
          </div>
          <a routerLink="/dashboard" routerLinkActive="is-active">Overview</a>
          <a routerLink="/requests" routerLinkActive="is-active" [routerLinkActiveOptions]="{ exact: true }">Requests</a>
          @if (session.isRequester()) {
            <a routerLink="/requests/new" routerLinkActive="is-active">New request</a>
          }
          @if (session.isApprover()) {
            <a routerLink="/approvals" routerLinkActive="is-active">Approval queue</a>
          }
          <div class="rail__user">
            <strong>{{ user.displayName }}</strong>
            <span>{{ user.departmentName }}</span>
            <span class="rail__roles">{{ roles() }}</span>
            <button type="button" class="link-button" (click)="switchUser()">Switch user</button>
          </div>
        </nav>
        <main id="main" class="content" tabindex="-1">
          <router-outlet />
        </main>
      </div>
    } @else {
      <main id="main" tabindex="-1"><router-outlet /></main>
    }
    <app-toast-host />
  `
})
export class AppComponent {
  protected readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly api = inject(BudgetApiService);

  protected readonly roles = computed(() => this.session.user()?.roles.join(' and ') ?? '');

  protected switchUser(): void {
    this.session.signOut();
    this.api.clearCache();
    void this.router.navigate(['/sign-in']);
  }
}
