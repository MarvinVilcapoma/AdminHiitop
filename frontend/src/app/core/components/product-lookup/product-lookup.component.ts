import { DecimalPipe } from '@angular/common';
import { Component, DestroyRef, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, Subject, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiService } from '../../services/api.service';
import { LoadingStateComponent } from '../loading-state/loading-state.component';
import { ProductLookupItem } from '../../models';

@Component({
  selector: 'app-product-lookup',
  standalone: true,
  imports: [FormsModule, DecimalPipe, LoadingStateComponent],
  templateUrl: './product-lookup.component.html',
  styleUrl: './product-lookup.component.scss',
})
export class ProductLookupComponent {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly search$ = new Subject<string>();

  warehouseId = input<number | null>(null);
  colorId = input<number | null>(null);
  availableOnly = input(true);
  limit = input(30);
  minLength = input(1);
  debounceMs = input(220);
  mode = input<'dropdown' | 'panel'>('dropdown');
  panelTitle = input('Resultados');
  resetKey = input(0);
  refreshKey = input(0);
  placeholder = input('Busca un producto');
  hint = input('Busca por nombre, SKU, color o talla.');
  emptyText = input('No se encontraron coincidencias.');
  disabled = input(false);

  selected = output<ProductLookupItem>();
  errorChange = output<string>();
  queryChange = output<string>();

  query = '';
  searching = signal(false);
  results = signal<ProductLookupItem[]>([]);
  opened = signal(false);

  constructor() {
    this.search$
      .pipe(
        debounceTime(this.debounceMs()),
        distinctUntilChanged(),
        switchMap((query) => {
          const warehouseId = this.warehouseId();
          const colorId = this.colorId();
          const availableOnly = this.availableOnly();
          const limit = this.limit();
          const params: Record<string, string | number | boolean> = {
            search: query,
            available_only: availableOnly ? 1 : 0,
            limit,
          };

          if (warehouseId !== null) {
            params['warehouse_id'] = warehouseId;
          }

          if (colorId !== null) {
            params['color_id'] = colorId;
          }

          this.searching.set(true);
          this.errorChange.emit('');

          return this.api.get<{ data: ProductLookupItem[] }>('stocks/lookup', params);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (response) => {
          this.results.set(Array.isArray(response?.data) ? response.data : []);
          this.searching.set(false);
          this.opened.set(true);
        },
        error: () => {
          this.results.set([]);
          this.searching.set(false);
          this.opened.set(true);
          this.errorChange.emit('No se pudo completar la busqueda de productos.');
        },
      });

    effect(() => {
      this.resetKey();
      this.query = '';
      this.resetResults(false);
    });

    effect(() => {
      this.refreshKey();

      if (this.query.trim().length >= this.minLength()) {
        this.onInputChange();
      }
    });
  }

  onInputChange(): void {
    const value = this.query.trim();
    this.queryChange.emit(this.query);

    if (this.disabled()) {
      this.resetResults(false);
      return;
    }

    if (value.length < this.minLength()) {
      this.resetResults(false);
      return;
    }

    this.search$.next(value);
  }

  selectItem(item: ProductLookupItem): void {
    this.selected.emit(item);
    this.query = '';
    this.resetResults(false);
    this.queryChange.emit('');
  }

  clear(): void {
    this.query = '';
    this.resetResults(false);
    this.queryChange.emit('');
    this.errorChange.emit('');
  }

  closeDropdown(): void {
    if (this.mode() === 'panel') {
      return;
    }

    setTimeout(() => this.opened.set(false), 120);
  }

  private resetResults(opened: boolean): void {
    this.results.set([]);
    this.searching.set(false);
    this.opened.set(opened);
  }
}
