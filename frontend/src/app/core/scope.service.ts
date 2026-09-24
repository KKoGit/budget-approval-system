import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { SessionService } from './session.service';

/**
 * The fiscal-year and department slice shared by the dashboard, the request list and the approval queue.
 * One instance, one ScopeBar component, one set of API parameters: switching to "FY2026 · Facilities" on
 * any screen shows the same slice on every other screen. The API enforces the same rules server-side
 * (BudgetScopePolicy), so a requester's department is locked here only for clarity, not for security.
 */
@Injectable({ providedIn: 'root' })
export class ScopeService {
  private readonly session = inject(SessionService);

  readonly fiscalYear = signal<number | null>(null);
  private readonly selectedDepartment = signal<number | null>(null);

  /** Requesters are always pinned to their own department. */
  readonly departmentLocked = computed(() => !this.session.isApprover());

  readonly departmentId = computed(() =>
    this.departmentLocked() ? this.session.user()?.departmentId ?? null : this.selectedDepartment());

  constructor() {
    // A persona switch must not carry the previous user's department filter across.
    effect(() => {
      this.session.user();
      this.selectedDepartment.set(null);
    }, { allowSignalWrites: true });
  }

  initialiseFiscalYear(current: number): void {
    if (this.fiscalYear() === null) this.fiscalYear.set(current);
  }

  setFiscalYear(value: number | null): void { this.fiscalYear.set(value); }

  setDepartment(value: number | null): void {
    if (!this.departmentLocked()) this.selectedDepartment.set(value);
  }
}
