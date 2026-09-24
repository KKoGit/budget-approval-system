import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { catchError, combineLatest, debounceTime, filter, map, of, startWith, switchMap, tap } from 'rxjs';
import { BudgetApiService } from '../../core/budget-api.service';
import { CATEGORY_LABELS } from '../../core/labels';
import { ApprovalQueueItem } from '../../core/models';
import { ScopeService } from '../../core/scope.service';
import { AllocationBarComponent } from '../../shared/allocation-bar.component';
import { ScopeBarComponent } from '../../shared/scope-bar.component';

type QueueState = { loading: true } | { loading: false; items: ApprovalQueueItem[]; failed: boolean };

/** Submitted requests, oldest first, within the shared fiscal-year / department scope. */
@Component({
  selector: 'app-approval-queue',
  standalone: true,
  imports: [CurrencyPipe, RouterLink, ScopeBarComponent, AllocationBarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './approval-queue.component.html'
})
export class ApprovalQueueComponent {
  private readonly api = inject(BudgetApiService);
  private readonly router = inject(Router);
  protected readonly scope = inject(ScopeService);
  protected readonly categoryLabels = CATEGORY_LABELS;

  protected readonly lookups = toSignal(this.api.lookups().pipe(tap(l => this.scope.initialiseFiscalYear(l.currentFiscalYear))));
  private readonly scopeKey = computed(() => ({ fy: this.scope.fiscalYear(), dept: this.scope.departmentId() }));

  protected readonly state = toSignal(
    combineLatest([toObservable(this.lookups), toObservable(this.scopeKey)]).pipe(
      filter(([l]) => !!l),
      debounceTime(0),
      switchMap(([, key]) => this.api.approvalQueue(key.fy, key.dept).pipe(
        map((items): QueueState => ({ loading: false, items, failed: false })),
        catchError(() => of<QueueState>({ loading: false, items: [], failed: true })),
        startWith<QueueState>({ loading: true })
      ))
    ),
    { initialValue: { loading: true } as QueueState }
  );

  protected readonly items = computed(() => { const s = this.state(); return s.loading ? [] : s.items; });
  protected readonly total = computed(() => this.items().reduce((sum, i) => sum + i.request.requestedAmount, 0));
  protected readonly decidable = computed(() => this.items().filter(i => !i.isOwnRequest).length);

  protected open(item: ApprovalQueueItem): void {
    void this.router.navigate(['/requests', item.request.id]);
  }
}
