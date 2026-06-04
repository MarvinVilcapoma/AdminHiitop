import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgClass } from '@angular/common';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import { PageStateComponent } from '../../../core/components';
import { ToastService } from '../../../core/services/toast.service';
import { AppUser } from '../../../core/models';

interface PermissionRow {
  id: number;
  name: string;
}

interface RoleRow {
  id: number;
  name: string;
  users_count?: number;
  guard_name?: string;
  permissions?: PermissionRow[];
}

interface RoleDraft {
  id: number;
  name: string;
  permissionIds: string[];
}

interface RoleDetailResponse {
  id: number;
  name: string;
  permissions?: PermissionRow[];
  permission_ids?: number[];
}

interface ModuleOption {
  permission: string;
  label: string;
  description: string;
  icon: string;
}

@Component({
  selector: 'app-roles-list',
  standalone: true,
  imports: [FormsModule, NgClass, RouterLink, PageStateComponent],
  templateUrl: './roles-list.component.html',
  styleUrl: './roles-list.component.scss',
})
export class RolesListComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);

  loading = signal(false);
  saving = signal(false);
  showForm = signal(false);
  formError = signal('');
  editError = signal('');

  roles = signal<RoleRow[]>([]);
  permissions = signal<PermissionRow[]>([]);

  newName = '';
  searchTerm = signal('');
  newPermissions = signal<string[]>([]);

  editRole = signal<RoleDraft | null>(null);

  delConfirm = signal<{ message: string; action: () => void } | null>(null);

  readonly moduleCatalog: ModuleOption[] = [
    { permission: 'dashboard.view',        label: 'Dashboard',      description: 'Inicio y métricas',                  icon: 'bi-grid-1x2'          },
    { permission: 'pos.view',              label: 'Punto de venta', description: 'Caja, emisión y cobro en tienda',    icon: 'bi-shop-window'        },
    { permission: 'orders.view',           label: 'Pedidos',        description: 'Gestión de pedidos y devoluciones',  icon: 'bi-bag'                },
    { permission: 'guides.view',           label: 'Guías',          description: 'Guías de remisión',                  icon: 'bi-truck'              },
    { permission: 'stocks.view',           label: 'Inventario',     description: 'Inventario y ajustes de stock',      icon: 'bi-boxes'              },
    { permission: 'customers.view',        label: 'Clientes',       description: 'Base de clientes',                   icon: 'bi-people'             },
    { permission: 'invoices.view',         label: 'Comprobantes',   description: 'Comprobantes electrónicos y SUNAT',  icon: 'bi-receipt'            },
    { permission: 'finance.view',          label: 'Finanzas',       description: 'Ingresos, gastos y finanzas',        icon: 'bi-bar-chart-line'     },
    { permission: 'users.view',            label: 'Usuarios',       description: 'Usuarios y roles',                   icon: 'bi-person-badge'       },
    { permission: 'config.order-statuses', label: 'Configuración',  description: 'Catálogos y ajustes',                icon: 'bi-gear'               },
  ];

  availableModules = computed(() => {
    const perms = new Set(this.permissions().map(p => p.name));
    return this.moduleCatalog.filter(m => perms.has(m.permission));
  });

  filteredRoles = computed(() => {
    const q = this.searchTerm().trim().toLowerCase();
    if (!q) return this.roles();

    return this.roles().filter(role => {
      const byName = role.name.toLowerCase().includes(q);
      const byModule = this.roleModules(role).some(m => m.label.toLowerCase().includes(q));
      return byName || byModule;
    });
  });

  totalRoles = computed(() => this.roles().length);
  rolesWithUsers = computed(() => this.roles().filter(r => (r.users_count ?? 0) > 0).length);
  totalVisibleModules = computed(() => this.availableModules().length);

  ngOnInit(): void {
    this.loadPermissions();
    this.loadRoles();
  }

  private normalizeArray<T>(response: T[] | { data?: T[] } | null | undefined): T[] {
    if (Array.isArray(response)) {
      return response;
    }

    if (Array.isArray(response?.data)) {
      return response.data;
    }

    return [];
  }

  private toPermissionIds(permissionNames: string[]): number[] {
    const catalog = new Map(this.permissions().map((permission) => [permission.name, permission.id]));

    return permissionNames
      .map((permissionName) => catalog.get(permissionName))
      .filter((permissionId): permissionId is number => typeof permissionId === 'number' && permissionId > 0);
  }

  loadPermissions(): void {
    this.api.get<PermissionRow[] | { data: PermissionRow[] }>('roles/permissions').subscribe({
      next: (rows) => this.permissions.set(this.normalizeArray(rows)),
      error: () => this.permissions.set([]),
    });
  }

  loadRoles(): void {
    this.loading.set(true);

    forkJoin({
      roles: this.api.get<RoleRow[] | { data: RoleRow[] }>('roles'),
      users: this.api.get<AppUser[] | { data: AppUser[] }>('users').pipe(
        catchError(() => of([] as AppUser[]))
      ),
    }).subscribe({
      next: ({ roles, users }) => {
        const baseRoles = this.normalizeArray(roles);
        const allUsers = this.normalizeArray(users);

        if (baseRoles.length === 0) {
          this.roles.set([]);
          this.loading.set(false);
          return;
        }

        const usersCountByRole = new Map<number, number>();
        allUsers.forEach((user) =>
          (user.roles ?? []).forEach((role) => {
            usersCountByRole.set(role.id, (usersCountByRole.get(role.id) ?? 0) + 1);
          })
        );

        forkJoin(
          baseRoles.map((role) =>
            this.api.get<RoleDetailResponse>(`roles/${role.id}`).pipe(
              catchError(() => of<RoleDetailResponse | null>(null))
            )
          )
        ).subscribe({
          next: (details) => {
            const detailByRoleId = new Map(
              details
                .filter((detail): detail is RoleDetailResponse => detail !== null)
                .map((detail) => [detail.id, detail])
            );

            this.roles.set(
              baseRoles.map((role) => ({
                ...role,
                permissions: detailByRoleId.get(role.id)?.permissions ?? role.permissions ?? [],
                users_count: usersCountByRole.get(role.id) ?? role.users_count ?? 0,
              }))
            );
            this.loading.set(false);
          },
          error: () => {
            this.roles.set(
              baseRoles.map((role) => ({
                ...role,
                users_count: usersCountByRole.get(role.id) ?? role.users_count ?? 0,
              }))
            );
            this.loading.set(false);
          },
        });
      },
      error: () => this.loading.set(false),
    });
  }

  toggleForm(): void {
    this.showForm.set(!this.showForm());
    this.formError.set('');
    this.newName = '';
    this.newPermissions.set([]);
  }

  toggleCreateModule(permission: string): void {
    const current = this.newPermissions();
    this.newPermissions.set(
      current.includes(permission)
        ? current.filter(p => p !== permission)
        : [...current, permission]
    );
  }

  create(): void {
    this.formError.set('');

    if (!this.newName.trim()) {
      this.formError.set('El nombre del rol es requerido.');
      return;
    }

    if (this.newPermissions().length === 0) {
      this.formError.set('Selecciona al menos un módulo para el rol.');
      return;
    }

    this.saving.set(true);
    const permissionIds = this.toPermissionIds(this.newPermissions());

    if (permissionIds.length !== this.newPermissions().length) {
      this.formError.set('No se pudieron resolver todos los permisos seleccionados.');
      this.saving.set(false);
      return;
    }

    this.api.post('roles', {
      name: this.newName.trim(),
      permission_ids: permissionIds,
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.newName = '';
        this.newPermissions.set([]);
        this.toast.success('Rol creado correctamente.');
        this.loadRoles();
      },
      error: (e) => {
        const msg = e?.error?.message ?? e?.error?.errors?.name?.[0] ?? 'Error al crear el rol.';
        const message = typeof msg === 'string' ? msg : JSON.stringify(msg);
        this.formError.set(message);
        this.saving.set(false);
        this.toast.error(message);
      },
    });
  }

  startEdit(role: RoleRow): void {
    this.editError.set('');
    this.api.get<any>(`roles/${role.id}`).subscribe({
      next: (full) => {
        const perms = (full?.permissions ?? []).map((p: PermissionRow) => p.name);
        this.editRole.set({ id: full.id, name: full.name, permissionIds: perms });
      },
      error: () => {
        this.editError.set('No se pudo cargar el detalle del rol.');
        this.toast.error('No se pudo cargar el detalle del rol.');
      },
    });
  }

  toggleEditModule(permission: string): void {
    const current = this.editRole();
    if (!current) return;

    const nextPermissions = current.permissionIds.includes(permission)
      ? current.permissionIds.filter(p => p !== permission)
      : [...current.permissionIds, permission];

    this.editRole.set({ ...current, permissionIds: nextPermissions });
  }

  saveEdit(): void {
    const draft = this.editRole();
    if (!draft) return;

    this.editError.set('');

    if (!draft.name.trim()) {
      this.editError.set('El nombre del rol es requerido.');
      return;
    }

    if (draft.permissionIds.length === 0) {
      this.editError.set('Selecciona al menos un módulo para el rol.');
      return;
    }

    this.saving.set(true);
    const permissionIds = this.toPermissionIds(draft.permissionIds);

    if (permissionIds.length !== draft.permissionIds.length) {
      this.editError.set('No se pudieron resolver todos los permisos seleccionados.');
      this.saving.set(false);
      return;
    }

    this.api.put(`roles/${draft.id}`, {
      name: draft.name.trim(),
      permission_ids: permissionIds,
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.editRole.set(null);
        this.toast.success('Rol actualizado correctamente.');
        this.loadRoles();
      },
      error: (e) => {
        const msg = e?.error?.message ?? e?.error?.errors?.name?.[0] ?? 'Error al guardar.';
        const message = typeof msg === 'string' ? msg : JSON.stringify(msg);
        this.editError.set(message);
        this.saving.set(false);
        this.toast.error(message);
      },
    });
  }

  closeEdit(): void {
    this.editRole.set(null);
    this.editError.set('');
  }

  delete(role: RoleRow): void {
    this.delConfirm.set({
      message: `¿Eliminar el rol "${role.name}"? Esta acción no se puede deshacer.`,
      action: () => {
        this.api.delete(`roles/${role.id}`).subscribe({
          next: () => {
            this.delConfirm.set(null);
            this.toast.success('Rol eliminado correctamente.');
            this.loadRoles();
          },
          error: (e) => this.toast.error(e?.error?.message ?? 'No se pudo eliminar el rol.'),
        });
      },
    });
  }

  roleModules(role: RoleRow): ModuleOption[] {
    const rolePermissions = new Set((role.permissions ?? []).map(p => p.name));
    return this.moduleCatalog.filter(m => rolePermissions.has(m.permission));
  }

  hasCreateModule(permission: string): boolean {
    return this.newPermissions().includes(permission);
  }

  hasEditModule(permission: string): boolean {
    return this.editRole()?.permissionIds.includes(permission) ?? false;
  }
}
