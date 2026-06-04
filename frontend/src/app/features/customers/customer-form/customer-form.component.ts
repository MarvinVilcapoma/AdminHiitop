import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Location } from '@angular/common';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { Province, District, Page } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';
import { SearchableSelectComponent, SelectOption } from '../../../core/components/searchable-select/searchable-select.component';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-customer-form',
  standalone: true,
  imports: [FormsModule, RouterLink, PageStateComponent, SearchableSelectComponent],
  templateUrl: './customer-form.component.html',
  styleUrl: './customer-form.component.scss',
})
export class CustomerFormComponent implements OnInit {
  private api      = inject(ApiService);
  private router   = inject(Router);
  private route    = inject(ActivatedRoute);
  private toast    = inject(ToastService);
  readonly location = inject(Location);

  loading   = signal(false);
  saving    = signal(false);
  isEdit    = signal(false);
  error     = signal('');
  provinces = signal<Province[]>([]);
  districts = signal<District[]>([]);
  provinceOptions = signal<SelectOption[]>([]);
  districtOptions = signal<SelectOption[]>([]);

  form: any = { full_name: '', dni: '', phone: '', email: '', province_id: '', district_id: '', address: '' };

  ngOnInit(): void {
    this.api.get<Page<Province>>('provinces?per_page=500').subscribe(r => {
      const provinces = r.data ?? (r as unknown as Province[]);
      this.provinces.set(provinces);
      this.provinceOptions.set(provinces.map(p => ({ id: p.id, name: p.name })));
    });
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.isEdit.set(true);
      this.loading.set(true);
      this.api.get<any>(`customers/${id}`).subscribe({
        next: c => {
          this.form = {
            full_name:   c.full_name   ?? '',
            dni:         c.dni         ?? '',
            phone:       c.phone       ?? '',
            email:       c.email       ?? '',
            province_id: c.province_id ?? '',
            district_id: c.district_id ?? '',
            address:     c.address     ?? '',
          };
          if (c.province_id) this.loadDistricts(c.province_id);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  onProvinceChange(): void {
    this.form.district_id = '';
    this.districts.set([]);
    this.districtOptions.set([]);
    if (this.form.province_id) this.loadDistricts(this.form.province_id);
  }

  private loadDistricts(provId: string | number): void {
    this.api.get<any>(`districts?province_id=${provId}&per_page=500`).subscribe(r => {
      const districts = r.data ?? r;
      this.districts.set(districts);
      this.districtOptions.set((districts as District[]).map(d => ({ id: d.id, name: d.name })));
    });
  }

  save(): void {
    this.error.set('');
    if (!this.form.full_name.trim()) { this.error.set('El nombre es requerido.'); return; }
    this.saving.set(true);
    const id = this.route.snapshot.paramMap.get('id');
    const req = this.isEdit()
      ? this.api.put(`customers/${id}`, this.form)
      : this.api.post('customers', this.form);
    req.subscribe({
      next:  () => {
        this.toast.success(this.isEdit() ? 'Cliente actualizado correctamente.' : 'Cliente creado correctamente.');
        this.location.back();
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
