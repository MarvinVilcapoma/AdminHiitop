import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { AppUser } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-user-form',
  standalone: true,
  imports: [FormsModule, RouterLink, PageStateComponent],
  templateUrl: './user-form.component.html',
  styleUrl: './user-form.component.scss',
})
export class UserFormComponent implements OnInit {
  private api    = inject(ApiService);
  private router = inject(Router);
  private route  = inject(ActivatedRoute);
  private toast  = inject(ToastService);

  loading        = signal(false);
  saving         = signal(false);
  isEdit         = signal(false);
  error          = signal('');
  availableRoles = signal<{ name: string; description: string; }[]>([]);
  selectedRole   = signal<string>('');

  form = { name: '', email: '', password: '', password_confirmation: '' };

  readonly roleDescriptions: Record<string, string> = {
    admin:    'Acceso total al sistema',
    manager:  'Gestión de pedidos, stock y reportes',
    vendedor: 'Ver clientes y crear pedidos',
  };

  ngOnInit(): void {
    this.api.get<{ id: number; name: string }[]>('users/roles-list').subscribe(roles => {
      this.availableRoles.set(
        roles.map(r => ({ name: r.name, description: this.roleDescriptions[r.name] ?? r.name }))
      );
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.isEdit.set(true);
      this.loading.set(true);
      this.api.get<any>(`users/${id}`).subscribe({
        next: (u: AppUser) => {
          this.form.name  = u.name  ?? '';
          this.form.email = u.email ?? '';
          this.selectedRole.set((u.roles ?? [])[0]?.name ?? '');
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  selectRole(name: string): void {
    this.selectedRole.set(name);
  }

  roleBadge(role: string): string {
    const map: Record<string, string> = { admin: 'bg-danger', manager: 'bg-warning text-dark', vendedor: 'bg-info text-dark' };
    return map[role] ?? 'bg-secondary';
  }

  save(): void {
    this.error.set('');
    if (!this.form.name || !this.form.email) {
      this.error.set('Nombre y email son requeridos.'); return;
    }
    if (!this.isEdit() && !this.form.password) {
      this.error.set('La contraseña es requerida.'); return;
    }
    if (this.form.password && this.form.password !== this.form.password_confirmation) {
      this.error.set('Las contraseñas no coinciden.'); return;
    }
    if (!this.selectedRole()) {
      this.error.set('Debes seleccionar un rol para el usuario.'); return;
    }

    this.saving.set(true);
    const payload: any = { ...this.form, roles: [this.selectedRole()] };
    if (!payload.password) { delete payload.password; delete payload.password_confirmation; }

    const id = this.route.snapshot.paramMap.get('id');
    const req = this.isEdit()
      ? this.api.put(`users/${id}`, payload)
      : this.api.post('users', payload);

    req.subscribe({
      next:  () => {
        this.toast.success(this.isEdit() ? 'Usuario actualizado correctamente.' : 'Usuario creado correctamente.');
        this.router.navigate(['/dashboard/users']);
      },
      error: (e) => {
        const msg = e?.error?.message ?? e?.error?.errors ?? 'Error al guardar.';
        const message = typeof msg === 'string' ? msg : JSON.stringify(msg);
        this.error.set(message);
        this.saving.set(false);
        this.toast.error(message);
      },
    });
  }
}
