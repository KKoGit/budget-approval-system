import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NotificationService } from '../core/notification.service';

@Component({
  selector: 'app-toast-host',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toasts" role="status" aria-live="polite">
      @for (toast of notifications.toasts(); track toast.id) {
        <div class="toast toast--{{ toast.kind }}">
          <span>{{ toast.text }}</span>
          <button type="button" class="toast__close" (click)="notifications.dismiss(toast.id)" aria-label="Dismiss">×</button>
        </div>
      }
    </div>
  `
})
export class ToastHostComponent {
  protected readonly notifications = inject(NotificationService);
}
