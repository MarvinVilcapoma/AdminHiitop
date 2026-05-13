import { Injectable, signal } from '@angular/core';

export type ToastType = 'success' | 'danger' | 'warning' | 'info';

export interface ToastItem {
  id: number;
  text: string;
  type: ToastType;
  duration: number;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toasts = signal<ToastItem[]>([]);
  private nextId = 1;

  success(text: string, duration = 3500): void {
    this.show(text, 'success', duration);
  }

  error(text: string, duration = 4500): void {
    this.show(text, 'danger', duration);
  }

  warning(text: string, duration = 4000): void {
    this.show(text, 'warning', duration);
  }

  info(text: string, duration = 3500): void {
    this.show(text, 'info', duration);
  }

  dismiss(id: number): void {
    this.toasts.update((items) => items.filter((item) => item.id !== id));
  }

  private show(text: string, type: ToastType, duration: number): void {
    const id = this.nextId++;
    this.toasts.update((items) => [...items, { id, text, type, duration }]);
    window.setTimeout(() => this.dismiss(id), duration);
  }
}
