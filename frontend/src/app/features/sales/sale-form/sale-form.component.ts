import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';
import { Stock } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';
import { ToastService } from '../../../core/services/toast.service';
import { formatPeruDate, formatPeruDateTimeLocal } from '../../../core/utils/peru-date.util';

interface ItemRow {
  sku: string;
  product_name: string;
  variant: string;
  other_attributes: string;
  brand: string;
  product_type: string;
  list_price: number;
  unit_net_price: number;
  unit_gross_price: number;
  quantity: number;
  total_gross: number;
  total_net: number;
  total_tax: number;
  discount_name: string;
  discount_gross: number;
  discount_pct: number;
  unit_cost_net: number;
  total_cost_net: number;
  margin: number;
  margin_pct: number;
  // UI-only autocomplete state
  _search: string;
  _open: boolean;
}

function emptyItem(): ItemRow {
  return {
    sku: '', product_name: '', variant: '', other_attributes: '',
    brand: '', product_type: '',
    list_price: 0, unit_net_price: 0, unit_gross_price: 0,
    quantity: 1, total_gross: 0, total_net: 0, total_tax: 0,
    discount_name: '', discount_gross: 0, discount_pct: 0,
    unit_cost_net: 0, total_cost_net: 0, margin: 0, margin_pct: 0,
    _search: '', _open: false,
  };
}

@Component({
  selector: 'app-sale-form',
  standalone: true,
  imports: [FormsModule, RouterLink, DecimalPipe, PageStateComponent],
  templateUrl: './sale-form.component.html',
  styleUrl: './sale-form.component.scss',
})
export class SaleFormComponent implements OnInit {
  private readonly api    = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route  = inject(ActivatedRoute);
  private readonly toast  = inject(ToastService);

  isEdit  = signal(false);
  loading = signal(false);
  saving  = signal(false);
  error   = signal('');

  branches = signal<string[]>([]);
  stocks   = signal<Stock[]>([]);

  header = {
    movement_type:    'venta',
    document_type_label: '',
    document_number:  '',
    series_number:    '',
    series_prefix:    '',
    issue_date:       this.today(),
    sale_datetime:    this.now(),
    branch:           '',
    seller:           '',
    customer_name:    '',
    customer_tax_id:  '',
    customer_email:   '',
    customer_address: '',
    customer_district: '',
    customer_province: '',
    customer_department: '',
    price_list:       'Lista de Precios Base',
    delivery_type:    'Entrega inmediata',
    currency:         'PEN',
  };

  items: ItemRow[] = [emptyItem()];

  private saleId: number | null = null;

  private today(): string {
    return formatPeruDate();
  }

  private now(): string {
    return formatPeruDateTimeLocal();
  }

  get totalGross(): number {
    return this.items.reduce((s, i) => s + (i.total_gross || 0), 0);
  }
  get totalNet(): number {
    return this.items.reduce((s, i) => s + (i.total_net || 0), 0);
  }
  get totalTax(): number {
    return this.items.reduce((s, i) => s + (i.total_tax || 0), 0);
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.saleId = +id;
      this.loading.set(true);
      this.api.get<any>(`sales/${id}`).subscribe({
        next: s => {
          const sale = s.data ?? s;
          Object.assign(this.header, {
            movement_type:       sale.movement_type ?? 'venta',
            document_type_label: sale.document_type_label ?? '',
            document_number:     sale.document_number ?? '',
            series_number:       sale.series_number ?? '',
            series_prefix:       sale.series_prefix ?? '',
            issue_date:          sale.issue_date ? formatPeruDate(sale.issue_date) : this.today(),
            sale_datetime:       sale.sale_datetime ? formatPeruDateTimeLocal(sale.sale_datetime) : this.now(),
            branch:              sale.branch ?? '',
            seller:              sale.seller ?? '',
            customer_name:       sale.customer_name ?? '',
            customer_tax_id:     sale.customer_tax_id ?? '',
            customer_email:      sale.customer_email ?? '',
            customer_address:    sale.customer_address ?? '',
            customer_district:   sale.customer_district ?? '',
            customer_province:   sale.customer_province ?? '',
            customer_department: sale.customer_department ?? '',
            price_list:          sale.price_list ?? '',
            delivery_type:       sale.delivery_type ?? '',
            currency:            sale.currency ?? 'PEN',
          });
          this.items = (sale.items ?? []).map((i: any) => ({
            sku: i.sku ?? '', product_name: i.product_name ?? '',
            variant: i.variant ?? '', other_attributes: i.other_attributes ?? '',
            brand: i.brand ?? '', product_type: i.product_type ?? '',
            list_price: +i.list_price || 0,
            unit_net_price: +i.unit_net_price || 0,
            unit_gross_price: +i.unit_gross_price || 0,
            quantity: +i.quantity || 1,
            total_gross: +i.total_gross || 0,
            total_net: +i.total_net || 0,
            total_tax: +i.total_tax || 0,
            discount_name: i.discount_name ?? '',
            discount_gross: +i.discount_gross || 0,
            discount_pct: +i.discount_pct || 0,
            unit_cost_net: +i.unit_cost_net || 0,
            total_cost_net: +i.total_cost_net || 0,
            margin: +i.margin || 0,
            margin_pct: +i.margin_pct || 0,
            _search: i.product_name ?? '',
            _open: false,
          }));
          if (!this.items.length) this.items = [emptyItem()];
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }

    this.api.get<any>('sales/branches').subscribe(r => this.branches.set(r));
    this.api.get<any>('stocks?per_page=500').subscribe(r => this.stocks.set(r.data ?? r));
  }

  addItem(): void { this.items.push(emptyItem()); }

  removeItem(i: number): void {
    if (this.items.length > 1) this.items.splice(i, 1);
  }

  calcItem(item: ItemRow): void {
    const gross = item.unit_gross_price * item.quantity;
    item.total_gross = +gross.toFixed(2);
    const net = gross / 1.18;
    item.total_net  = +net.toFixed(2);
    item.total_tax  = +(gross - net).toFixed(2);
    if (!item.unit_net_price) {
      item.unit_net_price = +(item.unit_gross_price / 1.18).toFixed(2);
    }
  }

  onSkuChange(item: ItemRow): void {
    const match = this.stocks().find(
      s => s.product?.sku?.toLowerCase() === item.sku.toLowerCase()
    );
    if (match) {
      item.product_name  = match.product?.name ?? item.product_name;
      item.variant       = [match.color?.name, match.size].filter(Boolean).join(' / ');
      item.unit_gross_price = +(Number(match.product?.base_price ?? 0) * 1.18).toFixed(2);
      this.calcItem(item);
    }
  }

  suggestionsFor(item: ItemRow): Stock[] {
    const q = item._search.trim().toLowerCase();
    if (!q) return [];
    return this.stocks()
      .filter(s =>
        s.product?.name?.toLowerCase().includes(q) ||
        s.product?.sku?.toLowerCase().includes(q)
      )
      .slice(0, 10);
  }

  selectSuggestion(item: ItemRow, stock: Stock): void {
    item.sku           = stock.product?.sku ?? '';
    item.product_name  = stock.product?.name ?? '';
    item.variant       = [stock.color?.name, stock.size].filter(Boolean).join(' / ');
    item.unit_gross_price = +(Number(stock.product?.base_price ?? 0) * 1.18).toFixed(2);
    item._search       = item.product_name;
    item._open         = false;
    this.calcItem(item);
  }

  closeDropdown(item: ItemRow): void {
    setTimeout(() => { item._open = false; }, 150);
  }

  save(): void {
    this.error.set('');
    if (!this.items.some(i => i.quantity > 0)) {
      this.error.set('Agrega al menos un item con cantidad > 0.');
      return;
    }
    this.saving.set(true);

    const payload = {
      ...this.header,
      total_gross: +this.totalGross.toFixed(2),
      total_net:   +this.totalNet.toFixed(2),
      total_tax:   +this.totalTax.toFixed(2),
      items: this.items.filter(i => i.quantity > 0).map(i => ({
        sku: i.sku, product_name: i.product_name, variant: i.variant,
        other_attributes: i.other_attributes, brand: i.brand, product_type: i.product_type,
        list_price: i.list_price, unit_net_price: i.unit_net_price,
        unit_gross_price: i.unit_gross_price, quantity: i.quantity,
        total_gross: i.total_gross, total_net: i.total_net, total_tax: i.total_tax,
        discount_name: i.discount_name, discount_gross: i.discount_gross,
        discount_pct: i.discount_pct, unit_cost_net: i.unit_cost_net,
        total_cost_net: i.total_cost_net, margin: i.margin, margin_pct: i.margin_pct,
      })),
    };

    const req = this.isEdit()
      ? this.api.put(`sales/${this.saleId}`, payload)
      : this.api.post('sales', payload);

    req.subscribe({
      next: () => {
        this.toast.success(this.isEdit() ? 'Venta actualizada correctamente.' : 'Venta creada correctamente.');
        this.router.navigate(['/dashboard/sales']);
      },
      error: e => {
        const msg = e?.error?.message ?? e?.error?.errors ?? 'Error al guardar.';
        const message = typeof msg === 'string' ? msg : JSON.stringify(msg);
        this.error.set(message);
        this.saving.set(false);
        this.toast.error(message);
      },
    });
  }
}
