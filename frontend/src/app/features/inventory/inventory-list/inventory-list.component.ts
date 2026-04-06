import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ApiService } from '../../../core/services/api.service';
import { Stock, Page } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';

@Component({
  selector: 'app-inventory-list',
  standalone: true,
  imports: [PageStateComponent],
  templateUrl: './inventory-list.component.html',
  styleUrl: './inventory-list.component.scss',
})
export class InventoryListComponent implements OnInit {
  private readonly api = inject(ApiService);
  rows = signal<Stock[]>([]);
  total = signal(0);
  loading = signal(true);
  pageSize = 15;
  currentPage = 1;
  totalPages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));
  pageRange = computed(() => {
    const pages: number[] = [];
    for (let i = Math.max(1, this.currentPage - 2); i <= Math.min(this.totalPages(), this.currentPage + 2); i++) pages.push(i);
    return pages;
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.get<Page<Stock>>('stocks', { per_page: this.pageSize, page: this.currentPage }).subscribe((res) => {
      this.rows.set(res.data ?? []);
      this.total.set(res.total ?? this.rows().length);
      this.loading.set(false);
    });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage = page;
    this.load();
  }
}
