import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { District, Province } from '../../../core/models';

@Component({
  selector: 'app-geo-settings',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './geo-settings.component.html',
  styleUrl: './geo-settings.component.scss',
})
export class GeoSettingsComponent implements OnInit {
  private readonly api = inject(ApiService);

  provinces = signal<Province[]>([]);
  districts = signal<District[]>([]);

  selectedProvince = signal<Province | null>(null);

  loadingProvinces = signal(false);
  loadingDistricts = signal(false);
  saving = signal(false);

  provinceSearch = '';
  districtSearch = '';

  provincePage = 1;
  provincePerPage = 15;
  provinceTotal = signal(0);

  districtPage = 1;
  districtPerPage = 20;
  districtTotal = signal(0);

  provinceModalOpen = signal(false);
  districtModalOpen = signal(false);

  editingProvinceId: number | null = null;
  editingDistrictId: number | null = null;

  provinceForm: { name: string; code: string } = { name: '', code: '' };
  districtForm: { name: string; code: string } = { name: '', code: '' };

  confirmDelete = signal<{ type: 'province' | 'district'; id: number; name: string } | null>(null);

  ngOnInit(): void {
    this.loadProvinces();
  }

  loadProvinces(): void {
    this.loadingProvinces.set(true);

    const params: Record<string, string | number> = {
      per_page: this.provincePerPage,
      page: this.provincePage,
      search: this.provinceSearch.trim(),
    };

    this.api.get<any>('provinces', params).subscribe({
      next: (res) => {
        const rows = res?.data ?? (Array.isArray(res) ? res : []);
        this.provinces.set(rows);
        this.provinceTotal.set(res?.total ?? rows.length ?? 0);

        const currentSelectedId = this.selectedProvince()?.id;
        const selected = rows.find((p: Province) => p.id === currentSelectedId) ?? rows[0] ?? null;
        this.selectedProvince.set(selected);

        if (selected) {
          this.districtPage = 1;
          this.loadDistricts();
        } else {
          this.districts.set([]);
          this.districtTotal.set(0);
        }

        this.loadingProvinces.set(false);
      },
      error: () => {
        this.loadingProvinces.set(false);
      },
    });
  }

  loadDistricts(): void {
    const province = this.selectedProvince();
    if (!province) {
      this.districts.set([]);
      this.districtTotal.set(0);
      return;
    }

    this.loadingDistricts.set(true);

    const params: Record<string, string | number> = {
      province_id: province.id,
      per_page: this.districtPerPage,
      page: this.districtPage,
      search: this.districtSearch.trim(),
    };

    this.api.get<any>('districts', params).subscribe({
      next: (res) => {
        const rows = res?.data ?? (Array.isArray(res) ? res : []);
        this.districts.set(rows);
        this.districtTotal.set(res?.total ?? rows.length ?? 0);
        this.loadingDistricts.set(false);
      },
      error: () => {
        this.loadingDistricts.set(false);
      },
    });
  }

  onProvinceSearch(): void {
    this.provincePage = 1;
    this.loadProvinces();
  }

  onDistrictSearch(): void {
    this.districtPage = 1;
    this.loadDistricts();
  }

  selectProvince(p: Province): void {
    if (this.selectedProvince()?.id === p.id) {
      return;
    }

    this.selectedProvince.set(p);
    this.districtSearch = '';
    this.districtPage = 1;
    this.loadDistricts();
  }

  // Province CRUD

  openProvinceCreate(): void {
    this.editingProvinceId = null;
    this.provinceForm = { name: '', code: '' };
    this.provinceModalOpen.set(true);
  }

  openProvinceEdit(p: Province): void {
    this.editingProvinceId = p.id;
    this.provinceForm = {
      name: p.name ?? '',
      code: p.code ?? '',
    };
    this.provinceModalOpen.set(true);
  }

  closeProvinceModal(): void {
    this.provinceModalOpen.set(false);
  }

  saveProvince(): void {
    if (!this.provinceForm.name.trim()) {
      return;
    }

    this.saving.set(true);

    const payload = {
      name: this.provinceForm.name.trim(),
      code: this.provinceForm.code.trim() || null,
      is_active: true,
    };

    const req = this.editingProvinceId
      ? this.api.put(`provinces/${this.editingProvinceId}`, payload)
      : this.api.post('provinces', payload);

    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.provinceModalOpen.set(false);
        this.loadProvinces();
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }

  // District CRUD

  openDistrictCreate(): void {
    if (!this.selectedProvince()) {
      return;
    }

    this.editingDistrictId = null;
    this.districtForm = { name: '', code: '' };
    this.districtModalOpen.set(true);
  }

  openDistrictEdit(d: District): void {
    this.editingDistrictId = d.id;
    this.districtForm = {
      name: d.name ?? '',
      code: d.code ?? '',
    };
    this.districtModalOpen.set(true);
  }

  closeDistrictModal(): void {
    this.districtModalOpen.set(false);
  }

  saveDistrict(): void {
    const province = this.selectedProvince();
    if (!province || !this.districtForm.name.trim()) {
      return;
    }

    this.saving.set(true);

    const payload = {
      province_id: province.id,
      name: this.districtForm.name.trim(),
      code: this.districtForm.code.trim() || null,
      is_active: true,
    };

    const req = this.editingDistrictId
      ? this.api.put(`districts/${this.editingDistrictId}`, payload)
      : this.api.post('districts', payload);

    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.districtModalOpen.set(false);
        this.loadDistricts();
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }

  requestDeleteProvince(p: Province): void {
    this.confirmDelete.set({ type: 'province', id: p.id, name: p.name });
  }

  requestDeleteDistrict(d: District): void {
    this.confirmDelete.set({ type: 'district', id: d.id, name: d.name });
  }

  cancelDelete(): void {
    this.confirmDelete.set(null);
  }

  executeDelete(): void {
    const del = this.confirmDelete();
    if (!del) {
      return;
    }

    this.saving.set(true);

    const endpoint = del.type === 'province' ? `provinces/${del.id}` : `districts/${del.id}`;
    this.api.delete(endpoint).subscribe({
      next: () => {
        this.saving.set(false);
        this.confirmDelete.set(null);

        if (del.type === 'province') {
          this.loadProvinces();
        } else {
          this.loadDistricts();
        }
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }

  provincePageCount(): number {
    return Math.max(1, Math.ceil(this.provinceTotal() / this.provincePerPage));
  }

  districtPageCount(): number {
    return Math.max(1, Math.ceil(this.districtTotal() / this.districtPerPage));
  }

  goProvincePage(page: number): void {
    if (page < 1 || page > this.provincePageCount()) {
      return;
    }

    this.provincePage = page;
    this.loadProvinces();
  }

  goDistrictPage(page: number): void {
    if (page < 1 || page > this.districtPageCount()) {
      return;
    }

    this.districtPage = page;
    this.loadDistricts();
  }
}
