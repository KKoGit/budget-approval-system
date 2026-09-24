import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { STATUS_LABELS } from '../core/labels';
import { RequestStatus } from '../core/models';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge badge--{{ status() }}">{{ label() }}</span>`
})
export class StatusBadgeComponent {
  readonly status = input.required<RequestStatus>();
  protected readonly label = computed(() => STATUS_LABELS[this.status()]);
}
