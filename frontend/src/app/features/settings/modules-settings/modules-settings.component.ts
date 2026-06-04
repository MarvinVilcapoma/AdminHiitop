import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { AppConfigService, ALL_MODULE_OPTIONS } from '../../../core/services/app-config.service';

@Component({
  selector: 'app-modules-settings',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './modules-settings.component.html',
})
export class ModulesSettingsComponent implements OnInit {
  private readonly api       = inject(ApiService);
  private readonly toast     = inject(ToastService);
  private readonly appConfig = inject(AppConfigService);

  // ALL_MODULE_OPTIONS includes sales/promotions for the codebase; hide them from the UI
  readonly hiddenFromUi = new Set(['sales.view', 'promotions.view']);
  readonly allModules = ALL_MODULE_OPTIONS.filter(m => !this.hiddenFromUi.has(m.permission));
  active  = signal<Set<string>>(new Set());
  saving  = signal(false);
  loading = signal(true);

  ngOnInit(): void {
    // Use what's already in app config (loaded on app start)
    const current = this.appConfig.activeModules();
    this.active.set(new Set(current ?? this.allModules.map(m => m.permission)));
    this.loading.set(false);
  }

  // These permissions control sub-routes and core navigation — always on
  readonly essentialPermissions = new Set([
    'dashboard.view', 'config.order-statuses', 'users.view', 'customers.view',
  ]);

  toggle(permission: string): void {
    if (this.essentialPermissions.has(permission)) return;
    const s = new Set(this.active());
    if (s.has(permission)) s.delete(permission);
    else s.add(permission);
    this.active.set(s);
  }

  isActive(permission: string): boolean { return this.active().has(permission); }
  isEssential(permission: string): boolean { return this.essentialPermissions.has(permission); }

  save(): void {
    this.saving.set(true);
    const value = [...this.active()].join(',');
    this.api.patch('settings', {
      settings: [{ key: 'active_modules', value }],
    }).subscribe({
      next: () => {
        this.saving.set(false);
        // Update the in-memory config so nav + guards reflect immediately
        this.appConfig.activeModules.set([...this.active()]);
        this.toast.success('Módulos guardados. Los cambios son inmediatos.');
      },
      error: (e) => {
        this.saving.set(false);
        this.toast.error(e?.error?.message ?? 'Error al guardar módulos.');
      },
    });
  }
}
