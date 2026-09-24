import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { fiscalYearLabel } from '../core/labels';
import { Lookups } from '../core/models';
import { ScopeService } from '../core/scope.service';

/** The shared fiscal-year / department filter. Rendered identically on the dashboard, list and queue. */
@Component({
  selector: 'app-scope-bar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="scope" role="group" aria-label="Fiscal year and department">
      <div class="segmented" role="radiogroup" aria-label="Fiscal year">
        @for (fy of lookups().fiscalYears; track fy) {
          <button type="button" role="radio" [attr.aria-checked]="scope.fiscalYear() === fy"
                  [class.is-on]="scope.fiscalYear() === fy" (click)="scope.setFiscalYear(fy)">
            {{ label(fy) }}@if (fy === lookups().currentFiscalYear) {<span class="segmented__hint"> current</span>}
          </button>
        }
        <button type="button" role="radio" [attr.aria-checked]="scope.fiscalYear() === null"
                [class.is-on]="scope.fiscalYear() === null" (click)="scope.setFiscalYear(null)">All years</button>
      </div>

      <label class="scope__dept">
        <span class="visually-hidden">Department</span>
        <select [disabled]="scope.departmentLocked()" [value]="scope.departmentId() ?? ''"
                (change)="onDepartment($any($event.target).value)">
          <option value="">All departments</option>
          @for (d of lookups().departments; track d.id) {
            <option [value]="d.id" [selected]="scope.departmentId() === d.id">{{ d.name }}</option>
          }
        </select>
      </label>
      @if (scope.departmentLocked()) {
        <span class="scope__note">Showing your department only</span>
      }
    </div>
  `
})
export class ScopeBarComponent {
  readonly lookups = input.required<Lookups>();
  protected readonly scope = inject(ScopeService);
  protected readonly label = fiscalYearLabel;

  protected onDepartment(value: string): void {
    this.scope.setDepartment(value ? Number(value) : null);
  }
}
