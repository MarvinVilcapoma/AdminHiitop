import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';

interface SizeMetric {
  size:     string;
  quantity: number;
}

interface CustomerMetrics {
  customer_id:  number;
  full_name:    string;
  phone?:       string;
  email?:       string;
  order_count:  number;
  total_spent:  number;
  top_sizes:    SizeMetric[];
}

@Component({
  selector: 'app-customer-metrics',
  standalone: true,
  imports: [DecimalPipe, RouterLink],
  templateUrl: './customer-metrics.component.html',
})
export class CustomerMetricsComponent implements OnInit {
  private readonly api   = inject(ApiService);
  private readonly toast = inject(ToastService);

  loading    = signal(false);
  items      = signal<CustomerMetrics[]>([]);
  top        = 20;
  totalSpent = computed(() => this.items().reduce((s, c) => s + c.total_spent, 0));

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.get<CustomerMetrics[]>(`customers/metrics?top=${this.top}`).subscribe({
      next: (data) => {
        this.items.set(data ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Error al cargar métricas.');
      },
    });
  }

  /** Bar width % relative to max spender */
  spentPct(amount: number): number {
    const max = this.items()[0]?.total_spent ?? 1;
    return max > 0 ? Math.round((amount / max) * 100) : 0;
  }
}
