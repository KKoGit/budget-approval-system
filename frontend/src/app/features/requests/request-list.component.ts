import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { catchError, combineLatest, debounceTime, distinctUntilChanged, filter, map, of, switchMap, tap } from 'rxjs';
import { BudgetApiService } from '../../core/budget-api.service';
import { CATEGORY_LABELS, STATUS_LABELS } from '../../core/labels';
import { BudgetCategory, RequestQuery, RequestStatus, SortField } from '../../core/models';
import { ScopeService } from '../../core/scope.service';
import { SessionService } from '../../core/session.service';
import { ScopeBarComponent } from '../../shared/scope-bar.component';
import { StatusBadgeComponent } from '../../shared/status-badge.component';

@Component({
  selector: 'app-request-list',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, RouterLink, ScopeBarComponent, StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './request-list.component.html'
})
export class RequestListComponent {
  private readonly api = inject(BudgetApiService);
  private readonly router = inject(Router);
  protected readonly scope = inject(ScopeService);
  protected readonly session = inject(SessionService);

  protected readonly statusLabels = STATUS_LABELS;
  protected readonly categoryLabels = CATEGORY_LABELS;

  protected readonly lookups = toSignal(this.api.lookups().pipe(tap(l => this.scope.initialiseFiscalYear(l.currentFiscalYear))));

  // Local filters. Fiscal year and department come from the shared ScopeService.
  protected readonly status = signal<RequestStatus | null>(null);
  protected readonly category = signal<BudgetCategory | null>(null);
  protected readonly search = signal('');
  protected readonly sortBy = signal<SortField>('updated');
  protected readonly sortDirection = signal<'asc' | 'desc'>('desc');
  protected readonly page = signal(1);
  protected readonly pageSize = 12;
  protected readonly loading = signal(true);

  private readonly query = computed<RequestQuery>(() => ({
    fiscalYear: this.scope.fiscalYear(),
    departmentId: this.scope.departmentId(),
    status: this.status(),
    category: this.category(),
    search: this.search().trim() || null,
    sortBy: this.sortBy(),
    sortDirection: this.sortDirection(),
    page: this.page(),
    pageSize: this.pageSize
  }));

  protected readonly result = toSignal(
    combineLatest([toObservable(this.lookups), toObservable(this.query)]).pipe(
      filter(([l]) => !!l),
      map(([, q]) => q),
      debounceTime(250), // typing in search should not fire a request per keystroke
      distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
      tap(() => this.loading.set(true)),
      switchMap(q => this.api.searchRequests(q).pipe(catchError(() => of(null)))),
      tap(() => this.loading.set(false))
    )
  );

  protected readonly hasFilters = computed(() => !!(this.status() || this.category() || this.search().trim()));

  protected setStatus(value: string): void { this.status.set((value || null) as RequestStatus | null); this.page.set(1); }
  protected setCategory(value: string): void { this.category.set((value || null) as BudgetCategory | null); this.page.set(1); }
  protected setSearch(value: string): void { this.search.set(value); this.page.set(1); }

  protected clearFilters(): void {
    this.status.set(null);
    this.category.set(null);
    this.search.set('');
    this.page.set(1);
  }

  protected sort(field: SortField): void {
    if (this.sortBy() === field) {
      this.sortDirection.update(d => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sortBy.set(field);
      this.sortDirection.set(field === 'title' || field === 'department' ? 'asc' : 'desc');
    }
    this.page.set(1);
  }

  protected ariaSort(field: SortField): 'ascending' | 'descending' | 'none' {
    if (this.sortBy() !== field) return 'none';
    return this.sortDirection() === 'asc' ? 'ascending' : 'descending';
  }

  protected open(id: number): void {
    void this.router.navigate(['/requests', id]);
  }
}
