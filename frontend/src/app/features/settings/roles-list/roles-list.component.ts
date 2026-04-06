import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgClass } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { PageStateComponent } from '../../../core/components';

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
  permissions: string[];
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
    { permission: 'dashboard.view', label: 'Dashboard', description: 'Inicio y métricas', icon: 'bi-grid-1x2' },
    { permission: 'orders.view', label: 'Pedidos', description: 'Gestión de pedidos', icon: 'bi-bag' },
    { permission: 'guides.view', label: 'Guías', description: 'Guías de remisión', icon: 'bi-truck' },
    { permission: 'products.view', label: 'Productos', description: 'Catálogo de productos', icon: 'bi-box-seam' },
    { permission: 'stocks.view', label: 'Stock', description: 'Inventario y ajustes', icon: 'bi-boxes' },
    { permission: 'customers.view', label: 'Clientes', description: 'Base de clientes', icon: 'bi-people' },
    { permission: 'sales.view', label: 'Ventas', description: 'Ventas, promociones y reportes', icon: 'bi-graph-up' },
    { permission: 'users.view', label: 'Usuarios', description: 'Usuarios y roles', icon: 'bi-person-badge' },
    { permission: 'config.order-statuses', label: 'Configuración', description: 'Catálogos y ajustes', icon: 'bi-gear' },
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

  loadPermissions(): void {
    this.api.get<PermissionRow[]>('roles/permissions').subscribe({
      next: (rows) => this.permissions.set(Array.isArray(rows) ? rows : []),
      error: () => this.permissions.set([]),
    });
  }

  loadRoles(): void {
    this.loading.set(true);
    this.api.get<RoleRow[]>('roles').subscribe({
      next: (r) => {
        this.roles.set(Array.isArray(r) ? r : (r as any).data ?? []);
        this.loading.set(false);
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
    this.api.post('roles', {
      name: this.newName.trim(),
      permissions: this.newPermissions(),
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.newName = '';
        this.newPermissions.set([]);
        this.loadRoles();
      },
      error: (e) => {
        const msg = e?.error?.message ?? e?.error?.errors?.name?.[0] ?? 'Error al crear el rol.';
        this.formError.set(typeof msg === 'string' ? msg : JSON.stringify(msg));
        this.saving.set(false);
      },
    });
  }

  startEdit(role: RoleRow): void {
    this.editError.set('');
    this.api.get<any>(`roles/${role.id}`).subscribe({
      next: (full) => {
        const perms = (full?.permissions ?? []).map((p: PermissionRow) => p.name);
        this.editRole.set({ id: full.id, name: full.name, permissions: perms });
      },
      error: () => {
        this.editError.set('No se pudo cargar el detalle del rol.');
      },
    });
  }

  toggleEditModule(permission: string): void {
    const current = this.editRole();
    if (!current) return;

    const nextPermissions = current.permissions.includes(permission)
      ? current.permissions.filter(p => p !== permission)
      : [...current.permissions, permission];

    this.editRole.set({ ...current, permissions: nextPermissions });
  }

  saveEdit(): void {
    const draft = this.editRole();
    if (!draft) return;

    this.editError.set('');

    if (!draft.name.trim()) {
      this.editError.set('El nombre del rol es requerido.');
      return;
    }

    if (draft.permissions.length === 0) {
      this.editError.set('Selecciona al menos un módulo para el rol.');
      return;
    }

    this.saving.set(true);
    this.api.put(`roles/${draft.id}`, {
      name: draft.name.trim(),
      permissions: draft.permissions,
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.editRole.set(null);
        this.loadRoles();
      },
      error: (e) => {
        const msg = e?.error?.message ?? e?.error?.errors?.name?.[0] ?? 'Error al guardar.';
        this.editError.set(typeof msg === 'string' ? msg : JSON.stringify(msg));
        this.saving.set(false);
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
            this.loadRoles();
          },
          error: (e) => alert(e?.error?.message ?? 'No se pudo eliminar el rol.'),
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
    return this.editRole()?.permissions.includes(permission) ?? false;
  }
}
