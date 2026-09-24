import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * A department's year on one line: approved spend (solid), pending requests (hatched) and what is left.
 * When pending demand exceeds what remains, the bar rescales so the shortfall fits inside the track:
 * a marker shows where the allocation ends and the excess is drawn in red beyond it.
 */
@Component({
  selector: 'app-allocation-bar',
  standalone: true,
  imports: [CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="alloc" [class.alloc--compact]="compact()">
      <div class="alloc__track" role="img" [attr.aria-label]="description()">
        <span class="alloc__budget" [style.width.%]="budgetPct()"></span>
        <span class="alloc__approved" [style.width.%]="approvedPct()"></span>
        <span class="alloc__pending" [style.width.%]="pendingPct()"></span>
        @if (overflowPct() > 0) {
          <span class="alloc__overflow" [style.width.%]="overflowPct()"></span>
          <span class="alloc__limit" [style.left.%]="budgetPct()"></span>
        }
      </div>
      @if (!compact()) {
        <div class="alloc__legend">
          <span><i class="key key--approved"></i>{{ approved() | currency:'USD':'symbol':'1.0-0' }} approved</span>
          <span><i class="key key--pending"></i>{{ pending() | currency:'USD':'symbol':'1.0-0' }} pending</span>
          @if (shortfall() > 0) {
            <span><i class="key key--overflow"></i>{{ shortfall() | currency:'USD':'symbol':'1.0-0' }} over</span>
          }
          <span class="alloc__left" [class.is-short]="shortfall() > 0">
            {{ remaining() | currency:'USD':'symbol':'1.0-0' }} left of {{ allocated() | currency:'USD':'symbol':'1.0-0' }}
          </span>
        </div>
      }
    </div>
  `
})
export class AllocationBarComponent {
  readonly allocated = input.required<number>();
  readonly approved = input.required<number>();
  readonly pending = input(0);
  readonly compact = input(false);

  protected readonly remaining = computed(() => this.allocated() - this.approved());
  /** Pending demand that doesn't fit in what remains. */
  protected readonly shortfall = computed(() => Math.max(0, this.pending() - Math.max(0, this.remaining())));

  /** The bar's full width represents the allocation, or the total demand when that is larger. */
  private readonly scale = computed(() => Math.max(this.allocated(), this.approved() + this.pending()));
  private readonly pct = (v: number) => (this.scale() > 0 ? (Math.max(0, v) / this.scale()) * 100 : 0);

  protected readonly budgetPct = computed(() => this.pct(this.allocated()));
  protected readonly approvedPct = computed(() => this.pct(this.approved()));
  protected readonly pendingPct = computed(() => this.pct(this.pending() - this.shortfall()));
  protected readonly overflowPct = computed(() => this.pct(this.shortfall()));

  protected readonly description = computed(() => {
    const share = (v: number) => (this.allocated() > 0 ? Math.round((v / this.allocated()) * 100) : 0);
    return `${share(this.approved())}% of allocation approved, ${share(this.pending())}% pending` +
      (this.shortfall() > 0 ? `; pending requests exceed what remains by $${this.shortfall().toLocaleString('en-US')}` : '');
  });
}
