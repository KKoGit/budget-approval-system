import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable, catchError, map, of, switchMap } from 'rxjs';
import { BudgetApiService } from '../../core/budget-api.service';
import { HasUnsavedChanges } from '../../core/guards';
import { CATEGORY_LABELS, STATUS_LABELS } from '../../core/labels';
import { ApiError, BudgetCategory, BudgetRequestDetail } from '../../core/models';
import { NotificationService } from '../../core/notification.service';
import { SessionService } from '../../core/session.service';

const MAX_AMOUNT = 10_000_000;
const JUSTIFICATION_MIN = 20;
const JUSTIFICATION_MAX = 2000;

/** Mirrors the server rule: money has at most two decimal places. */
export function twoDecimalPlaces(control: AbstractControl<number | null>): ValidationErrors | null {
  const value = control.value;
  if (value === null || value === undefined || isNaN(value)) return null;
  return Math.round(value * 100) === value * 100 ? null : { precision: true };
}

@Component({
  selector: 'app-request-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './request-form.component.html'
})
export class RequestFormComponent implements OnInit, HasUnsavedChanges {
  /** Bound from the route (withComponentInputBinding). Absent when creating. */
  readonly id = input<string>();

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(BudgetApiService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  protected readonly session = inject(SessionService);

  protected readonly categoryLabels = CATEGORY_LABELS;
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly limits = { max: MAX_AMOUNT, justificationMin: JUSTIFICATION_MIN, justificationMax: JUSTIFICATION_MAX };

  protected readonly lookups = toSignal(this.api.lookups());
  protected readonly existing = signal<BudgetRequestDetail | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly serverError = signal<ApiError | null>(null);
  protected readonly saving = signal(false);
  protected readonly isEdit = computed(() => !!this.id());

  protected readonly form = this.fb.group({
    fiscalYear: this.fb.control<number | null>(null, Validators.required),
    category: this.fb.control<BudgetCategory | null>(null, Validators.required),
    title: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(120)]),
    justification: this.fb.nonNullable.control('', [
      Validators.required, Validators.minLength(JUSTIFICATION_MIN), Validators.maxLength(JUSTIFICATION_MAX)
    ]),
    requestedAmount: this.fb.control<number | null>(null, [
      Validators.required, Validators.min(0.01), Validators.max(MAX_AMOUNT), twoDecimalPlaces
    ])
  });

  protected readonly formValue = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });
  protected readonly justificationLength = computed(() => (this.formValue().justification ?? '').trim().length);

  /** Remaining allocation for the chosen fiscal year, shown beside the amount so requesters can self-check. */
  protected readonly remaining = toSignal(
    this.form.controls.fiscalYear.valueChanges.pipe(
      switchMap(fy => {
        const dept = this.session.user()?.departmentId ?? null;
        return fy ? this.api.dashboard(fy, dept).pipe(map(d => d.remaining), catchError(() => of(null))) : of(null);
      })
    ),
    { initialValue: null }
  );

  private saved = false;

  ngOnInit(): void {
    const id = this.id();
    if (!id) {
      this.api.lookups().subscribe(l => this.form.patchValue({ fiscalYear: l.currentFiscalYear }));
      return;
    }

    this.form.controls.fiscalYear.disable(); // fiscal year is fixed once a request exists
    this.api.getRequest(Number(id)).subscribe({
      next: detail => {
        this.existing.set(detail);
        const s = detail.summary;
        this.form.patchValue({
          fiscalYear: s.fiscalYear,
          category: s.category,
          title: s.title,
          justification: detail.justification,
          requestedAmount: s.requestedAmount
        });
        this.form.markAsPristine();
        if (!detail.permissions.canEdit) this.form.disable();
      },
      error: (e: ApiError) => this.loadError.set(e.detail)
    });
  }

  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saved;
  }

  protected showError(name: 'fiscalYear' | 'category' | 'title' | 'justification' | 'requestedAmount'): boolean {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || c.dirty);
  }

  protected save(andSubmit: boolean): void {
    this.serverError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const payload = {
      fiscalYear: v.fiscalYear!,
      category: v.category!,
      title: v.title.trim(),
      justification: v.justification.trim(),
      requestedAmount: Number(v.requestedAmount)
    };

    const existing = this.existing();
    const save$: Observable<BudgetRequestDetail> = existing
      ? this.api.updateRequest(existing.summary.id, payload, existing.version)
      : this.api.createRequest(payload);

    // Save first, then submit using the version returned by the save, so both steps are concurrency-checked.
    const flow$ = andSubmit ? save$.pipe(switchMap(saved => this.api.submitRequest(saved.summary.id, saved.version))) : save$;

    this.saving.set(true);
    flow$.subscribe({
      next: result => {
        this.saved = true;
        this.notifications.success(andSubmit ? 'Request submitted for approval.' : 'Draft saved.');
        void this.router.navigate(['/requests', result.summary.id]);
      },
      error: (e: ApiError) => {
        this.saving.set(false);
        this.serverError.set(e);
      }
    });
  }
}
