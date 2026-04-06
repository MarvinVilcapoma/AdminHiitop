import {
  Component,
  ElementRef,
  forwardRef,
  HostListener,
  Input,
  OnChanges,
  signal,
  SimpleChanges,
} from '@angular/core';
import {
  ControlValueAccessor,
  FormsModule,
  NG_VALUE_ACCESSOR,
} from '@angular/forms';

export interface SelectOption {
  id: number | string;
  name: string;
  hex_code?: string;
}

@Component({
  selector: 'app-searchable-select',
  standalone: true,
  imports: [FormsModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SearchableSelectComponent),
      multi: true,
    },
  ],
  templateUrl: './searchable-select.component.html',
  styleUrl: './searchable-select.component.scss',
})
export class SearchableSelectComponent implements ControlValueAccessor, OnChanges {

  @Input() options: SelectOption[] = [];
  @Input() placeholder = '— Selecciona —';
  @Input() searchPlaceholder = 'Buscar...';
  @Input() inputId = '';

  open   = signal(false);
  query  = '';
  filtered = signal<SelectOption[]>([]);

  value: number | string | null = null;
  disabled = false;

  private onChange: (v: number | string | null) => void = () => {};
  private onTouched: () => void = () => {};

  constructor(private elRef: ElementRef) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['options']) this.applyFilter();
  }

  get selectedOption(): () => SelectOption | undefined {
    return () => this.options.find(o => o.id === this.value);
  }

  toggle(): void {
    if (this.disabled) return;
    this.open.update(v => !v);
    if (this.open()) {
      this.query = '';
      this.applyFilter();
    }
  }

  onQueryChange(_: string): void { this.applyFilter(); }

  clearQuery(): void {
    this.query = '';
    this.applyFilter();
  }

  applyFilter(): void {
    const q = this.query.toLowerCase();
    this.filtered.set(
      q ? this.options.filter(o => o.name.toLowerCase().includes(q)) : [...this.options]
    );
  }

  select(opt: SelectOption | null): void {
    this.value = opt?.id ?? null;
    this.onChange(this.value);
    this.onTouched();
    this.open.set(false);
    this.query = '';
    this.applyFilter();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(e: MouseEvent): void {
    if (!this.elRef.nativeElement.contains(e.target)) {
      this.open.set(false);
    }
  }

  // ── ControlValueAccessor ──
  writeValue(val: number | string | null): void { this.value = val ?? null; }
  registerOnChange(fn: (v: number | string | null) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this.disabled = isDisabled; }
}
