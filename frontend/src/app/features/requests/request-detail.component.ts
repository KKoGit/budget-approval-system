import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { BudgetApiService } from '../../core/budget-api.service';
import { ACTION_LABELS, CATEGORY_LABELS, STATUS_LABELS } from '../../core/labels';
import { ApiError, BudgetRequestDetail, DecisionType } from '../../core/models';
import { NotificationService } from '../../core/notification.service';
import { AllocationBarComponent } from '../../shared/allocation-bar.component';
import { StatusBadgeComponent } from '../../shared/status-badge.component';
import { twoDecimalPlaces } from './request-form.component';

const DECISION_MESSAGES: Record<DecisionType, string> = {
  Approve: 'Request approved.',
  Reject: 'Request rejected.',
  Return: 'Request returned to the requester for revision.'
};

/**
 * One request: its facts, the actions the current user may take, and the full audit trail.
 * The permissions block comes from the API, so the page never guesses what the server will allow;
 * client-side checks here only give earlier, friendlier feedback.
 */
@Component({
  selector: 'app-request-detail',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, RouterLink, ReactiveFormsModule, StatusBadgeComponent, AllocationBarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './request-detail.component.html'
})
export class RequestDetailComponent {
  /** Bound from the route (withComponentInputBinding). */
  readonly id = input.required<string>();

  private readonly api = inject(BudgetApiService);
  private readonly fb = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);

  protected readonly statusLabels = STATUS_LABELS;
  protected readonly categoryLabels = CATEGORY_LABELS;
  protected readonly actionLabels = ACTION_LABELS;

  protected readonly detail = signal<BudgetRequestDetail | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly actionError = signal<ApiError | null>(null);
  protected readonly busy = signal(false);

  protected readonly decisionForm = this.fb.group({
    approvedAmount: this.fb.control<number | null>(null, [Validators.required, Validators.min(0.01), twoDecimalPlaces]),
    comment: this.fb.nonNullable.control('', Validators.maxLength(1000))
  });

  private readonly decisionValue = toSignal(this.decisionForm.valueChanges, { initialValue: this.decisionForm.getRawValue() });

  /** Client-side mirror of the domain rules so the approver sees problems before pressing a button. */
  protected readonly decisionHints = computed(() => {
    const d = this.detail();
    const v = this.decisionValue();
    if (!d) return { partial: false, overRequested: false, overRemaining: false };
    const amount = Number(v.approvedAmount ?? 0);
    return {
      partial: amount > 0 && amount < d.summary.requestedAmount,
      overRequested: amount > d.summary.requestedAmount,
      overRemaining: !!d.allocation && amount > d.allocation.remaining
    };
  });


  constructor() {
    effect(() => this.load(Number(this.id())), { allowSignalWrites: true });
  }

  protected load(id: number): void {
    this.loadError.set(null);
    this.actionError.set(null);
    this.api.getRequest(id).subscribe({
      next: detail => this.setDetail(detail),
      error: (e: ApiError) => this.loadError.set(e.status === 404 ? 'This request does not exist or is outside your department.' : e.detail)
    });
  }

  protected reload(): void {
    this.load(Number(this.id()));
  }

  protected submit(): void {
    const d = this.detail();
    if (!d) return;
    this.run(this.api.submitRequest(d.summary.id, d.version), 'Request submitted for approval.');
  }

  protected decide(decision: DecisionType): void {
    const d = this.detail();
    if (!d) return;
    this.actionError.set(null);
    const v = this.decisionForm.getRawValue();
    const comment = v.comment.trim();
    const hints = this.decisionHints();

    // These mirror COMMENT_REQUIRED / APPROVED_AMOUNT_INVALID; the API remains the authority.
    if (decision !== 'Approve' && !comment) {
      this.decisionForm.controls.comment.markAsTouched();
      this.actionError.set(new ApiError(422, 'COMMENT_REQUIRED', 'Comment required',
        'Explain why, so the requester knows what to change or why it was declined.'));
      return;
    }
    if (decision === 'Approve') {
      if (this.decisionForm.controls.approvedAmount.invalid || hints.overRequested) {
        this.decisionForm.controls.approvedAmount.markAsTouched();
        this.actionError.set(new ApiError(422, 'APPROVED_AMOUNT_INVALID', 'Invalid amount',
          'The approved amount must be greater than zero and no more than the requested amount.'));
        return;
      }
      if (hints.partial && !comment) {
        this.actionError.set(new ApiError(422, 'COMMENT_REQUIRED', 'Comment required',
          'Add a comment explaining the partial approval.'));
        return;
      }
    }

    this.run(this.api.decide(d.summary.id, {
      decision,
      approvedAmount: decision === 'Approve' ? Number(v.approvedAmount) : null,
      comment: comment || null,
      version: d.version
    }), DECISION_MESSAGES[decision]);
  }

  private run(action$: ReturnType<BudgetApiService['getRequest']>, message: string): void {
    this.busy.set(true);
    this.actionError.set(null);
    action$.subscribe({
      next: detail => {
        this.busy.set(false);
        this.setDetail(detail);
        this.notifications.success(message);
      },
      error: (e: ApiError) => {
        this.busy.set(false);
        this.actionError.set(e);
      }
    });
  }

  private setDetail(detail: BudgetRequestDetail): void {
    this.detail.set(detail);
    this.decisionForm.reset({ approvedAmount: detail.summary.requestedAmount, comment: '' });
  }
}
