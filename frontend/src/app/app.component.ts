import { DOCUMENT } from '@angular/common';
import { Component, OnDestroy, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastHostComponent } from './core/components/toast-host/toast-host.component';
import { AuthService } from './core/services/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, ToastHostComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly document = inject(DOCUMENT);
  private removeWheelListener: (() => void) | null = null;
  private removeSearchPasteListener: (() => void) | null = null;
  private readonly wheelBridgeSelector = [
    '.product-lookup__item',
    '.pos-customer-result',
    '.ss-option',
    '.ss-selected',
    '.color-btn',
    'button',
    '[role="button"]',
    'a.btn',
  ].join(', ');
  private readonly searchPasteSelector = [
    '[data-search-trigger]',
    'input[type="search"]',
    'input[name*="search" i]',
    'input[id*="search" i]',
    'input[class*="search" i]',
    'input[placeholder*="buscar" i]',
  ].join(', ');

  ngOnInit(): void {
    this.installWheelScrollBridge();
    this.installSearchPasteBridge();

    if (!this.auth.isAuthenticated()) {
      return;
    }

    this.auth.refreshMe().subscribe({
      error: () => this.auth.logout(),
    });
  }

  ngOnDestroy(): void {
    this.removeWheelListener?.();
    this.removeSearchPasteListener?.();
  }

  private installWheelScrollBridge(): void {
    const handler = (event: WheelEvent) => {
      if (event.defaultPrevented || event.ctrlKey) {
        return;
      }

      const target = event.target;
      if (!(target instanceof Element)) {
        return;
      }

      const interactiveTarget = target.closest(this.wheelBridgeSelector);
      if (!interactiveTarget) {
        return;
      }

      const scrollContainer = this.findScrollableAncestor(target);
      if (!scrollContainer) {
        return;
      }

      const maxScrollTop = scrollContainer.scrollHeight - scrollContainer.clientHeight;
      if (maxScrollTop <= 0) {
        return;
      }

      const deltaY = this.normalizeWheelDelta(event, scrollContainer);
      const nextScrollTop = scrollContainer.scrollTop + deltaY;
      const canScrollDown = deltaY > 0 && scrollContainer.scrollTop < maxScrollTop;
      const canScrollUp = deltaY < 0 && scrollContainer.scrollTop > 0;

      if (!canScrollDown && !canScrollUp) {
        return;
      }

      event.preventDefault();
      scrollContainer.scrollTo({
        top: Math.max(0, Math.min(maxScrollTop, nextScrollTop)),
        behavior: 'auto',
      });
    };

    this.document.addEventListener('wheel', handler, { capture: true, passive: false });
    this.removeWheelListener = () =>
      this.document.removeEventListener('wheel', handler, { capture: true } as EventListenerOptions);
  }

  private findScrollableAncestor(start: Element): HTMLElement | null {
    let current: Element | null = start;

    while (current) {
      if (current instanceof HTMLElement && this.isScrollable(current)) {
        if (current === this.document.body || current === this.document.documentElement) {
          return null;
        }

        return current;
      }

      current = current.parentElement;
    }

    return null;
  }

  private isScrollable(element: HTMLElement): boolean {
    const style = window.getComputedStyle(element);
    const overflowY = style.overflowY;
    const isOverflowScrollable = overflowY === 'auto' || overflowY === 'scroll' || overflowY === 'overlay';
    return isOverflowScrollable && element.scrollHeight > element.clientHeight + 1;
  }

  private normalizeWheelDelta(event: WheelEvent, container: HTMLElement): number {
    if (event.deltaMode === WheelEvent.DOM_DELTA_LINE) {
      return event.deltaY * 16;
    }

    if (event.deltaMode === WheelEvent.DOM_DELTA_PAGE) {
      return event.deltaY * container.clientHeight;
    }

    return event.deltaY;
  }

  private installSearchPasteBridge(): void {
    const handler = (event: ClipboardEvent) => {
      const target = event.target;
      if (!(target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement)) {
        return;
      }

      const searchInput = target.matches(this.searchPasteSelector)
        ? target
        : target.closest(this.searchPasteSelector);

      if (!(searchInput instanceof HTMLInputElement || searchInput instanceof HTMLTextAreaElement)) {
        return;
      }

      if (searchInput.disabled || searchInput.readOnly) {
        return;
      }

      window.setTimeout(() => {
        searchInput.dispatchEvent(new Event('input', { bubbles: true }));
        searchInput.dispatchEvent(new Event('change', { bubbles: true }));

        if (!searchInput.hasAttribute('data-search-trigger-enter')) {
          return;
        }

        const enterOptions: KeyboardEventInit = {
          bubbles: true,
          cancelable: true,
          key: 'Enter',
          code: 'Enter',
        };

        searchInput.dispatchEvent(new KeyboardEvent('keydown', enterOptions));
        searchInput.dispatchEvent(new KeyboardEvent('keyup', enterOptions));
      }, 0);
    };

    this.document.addEventListener('paste', handler, { capture: true });
    this.removeSearchPasteListener = () =>
      this.document.removeEventListener('paste', handler, { capture: true } as EventListenerOptions);
  }
}
