import { Component, input, output } from '@angular/core';
import { LoadingStateComponent } from '../loading-state/loading-state.component';

/**
 * Reusable page-state shell.
 *
 * Usage:
 *   <app-page-state [loading]="loading()" [error]="error()" (dismissed)="error.set('')">
 *     <!-- your content here -->
 *   </app-page-state>
 *
 * - loading=true  → shows a centered Bootstrap spinner
 * - error (non-empty) → shows a dismissible danger alert above ng-content
 * - otherwise      → projects ng-content as-is
 */
@Component({
  selector: 'app-page-state',
  standalone: true,
  imports: [LoadingStateComponent],
  template: `
    @if (loading()) {
      <app-loading-state mode="page" text="Cargando información..." />
    } @else {
      @if (error()) {
        <div class="alert alert-danger d-flex align-items-center gap-2 mb-3">
          <i class="bi bi-exclamation-triangle-fill flex-shrink-0"></i>
          <span>{{ error() }}</span>
          <button type="button" class="btn-close ms-auto" (click)="dismissed.emit()"></button>
        </div>
      }
      <ng-content />
    }
  `,
})
export class PageStateComponent {
  /** Show a spinner instead of content while true. */
  loading = input<boolean>(false);

  /**
   * When non-empty, a dismissible danger alert is rendered above the content.
   * Pass (dismissed)="error.set('')" to wire up the close button.
   */
  error     = input<string>('');
  dismissed = output<void>();
}
