import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, combineLatest, debounceTime, filter, of, switchMap, tap } from 'rxjs';
import { BudgetApiService } from '../../core/budget-api.service';
import { STATUS_LABELS, fiscalYearLabel } from '../../core/labels';
import { RequestStatus } from '../../core/models';
import { ScopeService } from '../../core/scope.service';
import { SessionService } from '../../core/session.service';
import { AllocationBarComponent } from '../../shared/allocation-bar.component';
import { ScopeBarComponent } from '../../shared/scope-bar.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CurrencyPipe, RouterLink, ScopeBarComponent, AllocationBarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent {
  private readonly api = inject(BudgetApiService);
  protected readonly scope = inject(ScopeService);
  protected readonly session = inject(SessionService);

  protected readonly lookups = toSignal(this.api.lookups().pipe(tap(l => this.scope.initialiseFiscalYear(l.currentFiscalYear))));

  private readonly scopeKey = computed(() => ({ fy: this.scope.fiscalYear(), dept: this.scope.departmentId() }));

  protected readonly data = toSignal(
    combineLatest([toObservable(this.lookups), toObservable(this.scopeKey)]).pipe(
      filter(([l]) => !!l),
      debounceTime(0),
      switchMap(([, key]) => this.api.dashboard(key.fy, key.dept).pipe(catchError(() => of(null))))
    )
  );

  protected readonly heading = computed(() => {
    const fy = fiscalYearLabel(this.scope.fiscalYear());
    const deptId = this.scope.departmentId();
    const dept = deptId ? this.lookups()?.departments.find(d => d.id === deptId)?.name : 'All departments';
    return `${fy}, ${dept ?? ''}`;
  });

  protected readonly usedPct = computed(() => {
    const d = this.data();
    return d && d.allocated > 0 ? Math.round((d.approved / d.allocated) * 100) : 0;
  });

  protected readonly statusRows = computed(() => {
    const d = this.data();
    if (!d) return [];
    const order: RequestStatus[] = ['Draft', 'Submitted', 'ReturnedForRevision', 'Approved', 'Rejected'];
    return order.map(s => ({ status: s, label: STATUS_LABELS[s], count: d.countsByStatus[s] ?? 0 }));
  });

  protected readonly overcommitted = computed(() =>
    (this.data()?.departments ?? []).filter(d => d.pending > d.remaining));

}
