import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { ApiService } from '../../core/services/api.service';

interface WarehouseRow {
  id: number;
  name: string;
  type?: string;
  is_pos?: boolean;
  warehouse_type?: { id: number; name: string; code?: string };
}

interface PaymentMethodRow {
  id: number;
  name: string;
}

interface DocumentTypeRow {
  id: number;
  name: string;
  code?: string;
}

interface ColorRow {
  id: number;
  name: string;
  hex_code?: string;
}

interface PosVariant {
  stock_id: number;
  product_id: number;
  product_name: string;
  sku: string;
  color_id: number | null;
  size: string | null;
  available_qty: number;
  unit_price: number;
  unit_cost: number;
  variant_label: string;
}

interface PosLine {
  stock_id: number;
  product_id: number;
  product_name: string;
  variant_label: string;
  color_id: number | null;
  size: string | null;
  quantity: number;
  unit_price: number;
  subtotal: number;
  available_qty: number;
  unit_cost: number;
}

interface PosPrintLine {
  product_name: string;
  variant_label?: string;
  quantity: number;
  unit_price: number;
  subtotal: number;
}

type PosPrintMode = 'a4' | 'ticket';

interface PosPrintFormatOption {
  id: string;
  label: string;
  description: string;
  mode: PosPrintMode;
  widthMm?: number;
}

interface PosPrintPayload {
  orderNumber: string;
  paymentMethod: string;
  documentType: string;
  total: number;
  lines: PosPrintLine[];
  customerName: string | null;
  customerDoc: string | null;
  printFormat: PosPrintFormatOption;
  storeName: string;
}

@Component({
  selector: 'app-pos',
  standalone: true,
  imports: [FormsModule, DecimalPipe, RouterLink],
  templateUrl: './pos.component.html',
  styleUrl: './pos.component.scss',
})
export class PosComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly posAllowedDocCodes = new Set(['BOLETA', 'FACTURA', 'TICKET', 'NOTA_VENTA']);

  loading = signal(false);
  saving = signal(false);
  searching = signal(false);
  btTesting = signal(false);

  error = signal('');
  success = signal('');
  info = signal('');

  warehouses = signal<WarehouseRow[]>([]);
  paymentMethods = signal<PaymentMethodRow[]>([]);
  documentTypes = signal<DocumentTypeRow[]>([]);
  colors = signal<ColorRow[]>([]);
  searchResults = signal<PosVariant[]>([]);

  selectedWarehouseId: number | null = null;
  selectedPaymentMethodId: number | null = null;
  selectedDocumentTypeId: number | null = null;
  selectedColorId: number | null = null;
  selectedPrintFormatId = 'BOLETA_T';

  includeCustomerData = false;
  printAfterSave = true;

  customerName = '';
  customerDni = '';
  customerPhone = '';
  customerAddress = '';
  note = '';
  productSearch = '';

  lines = signal<PosLine[]>([]);
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  selectedWarehouseName = computed(() =>
    this.warehouses().find((w) => w.id === this.selectedWarehouseId)?.name ?? 'Sin almacén activo'
  );

  hasMultiplePosStores = computed(() => this.warehouses().length > 1);

  selectedDocumentTypeName = computed(() =>
    this.documentTypes().find((d) => d.id === this.selectedDocumentTypeId)?.name ?? 'Sin documento'
  );

  lineCount = computed(() => this.lines().reduce((sum, line) => sum + line.quantity, 0));
  total = computed(() => +(this.lines().reduce((sum, line) => sum + line.subtotal, 0)).toFixed(2));
  totalCost = computed(() => +(this.lines().reduce((sum, line) => sum + (line.quantity * line.unit_cost), 0)).toFixed(2));
  totalProfit = computed(() => +(this.total() - this.totalCost()).toFixed(2));

  ngOnInit(): void {
    this.loadCatalogs();
  }

  loadCatalogs(): void {
    this.loading.set(true);

    forkJoin({
      warehouses: this.api.get<any>('warehouses?active_only=1&pos_only=1&per_page=200').pipe(catchError(() => of({ data: [] }))),
      docs: this.api.get<any>('document-types?active_only=1&per_page=200').pipe(catchError(() => of({ data: [] }))),
      methods: this.api.get<any>('payment-methods?per_page=200').pipe(catchError(() => of({ data: [] }))),
      colors: this.api.get<any>('colors?active_only=1&per_page=300').pipe(catchError(() => of({ data: [] }))),
      settings: this.api.get<any>('settings').pipe(catchError(() => of({}))),
    }).subscribe({
      next: ({ warehouses, docs, methods, colors, settings }) => {
        const warehouseRows: WarehouseRow[] = warehouses?.data ?? (Array.isArray(warehouses) ? warehouses : []);
        const rawDocRows: DocumentTypeRow[] = docs?.data ?? (Array.isArray(docs) ? docs : []);
        const docRows = rawDocRows.filter((d) => this.isPosDocumentType(d));
        const paymentRows: PaymentMethodRow[] = methods?.data ?? (Array.isArray(methods) ? methods : []);
        const colorRows: ColorRow[] = colors?.data ?? (Array.isArray(colors) ? colors : []);

        this.warehouses.set(warehouseRows);
        this.documentTypes.set(docRows);
        this.paymentMethods.set(paymentRows);
        this.colors.set(colorRows);

        this.selectedWarehouseId = this.resolveDefaultWarehouseId(warehouseRows, settings);

        const noteSale = docRows.find((d) => String(d.code ?? '').toUpperCase() === 'NOTA_VENTA');
        const factura = docRows.find((d) => String(d.code ?? '').toUpperCase() === 'FACTURA');
        const boleta = docRows.find((d) => String(d.code ?? '').toUpperCase() === 'BOLETA');
        const ticket = docRows.find((d) => String(d.code ?? '').toUpperCase() === 'TICKET');
        this.selectedDocumentTypeId = noteSale?.id ?? factura?.id ?? boleta?.id ?? ticket?.id ?? docRows[0]?.id ?? null;
        this.onDocumentTypeChange(true);
        this.selectedPaymentMethodId = paymentRows[0]?.id ?? null;

        if (!this.selectedWarehouseId) {
          this.error.set('No hay almacenes activos marcados como punto de venta. Configúralo en Ajustes > Almacenes.');
        }

        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('No se pudo cargar la configuración del POS.');
      },
    });
  }

  private resolveDefaultWarehouseId(rows: WarehouseRow[], settings: any): number | null {
    const configuredId = Number(settings?.pos_default_warehouse_id?.value ?? 0);
    if (configuredId > 0 && rows.some((w) => w.id === configuredId)) {
      return configuredId;
    }

    const firstStore = rows.find((w) => {
      const directType = String(w.type ?? '').toLowerCase();
      const typeCode = String(w.warehouse_type?.code ?? '').toLowerCase();
      const typeName = String(w.warehouse_type?.name ?? '').toLowerCase();

      return (
        directType === 'store' ||
        typeCode.includes('store') ||
        typeCode.includes('tienda') ||
        typeName.includes('tienda')
      );
    });

    return firstStore?.id ?? rows[0]?.id ?? null;
  }

  private isPosDocumentType(doc: DocumentTypeRow): boolean {
    return this.posAllowedDocCodes.has(String(doc.code ?? '').toUpperCase());
  }

  onProductSearchInput(): void {
    this.error.set('');

    const query = this.productSearch.trim();
    const hasColorFilter = !!this.selectedColorId;
    if (query.length < 2 && !hasColorFilter) {
      this.searchResults.set([]);
      this.searching.set(false);
      return;
    }

    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }

    this.searchTimer = setTimeout(() => this.searchVariants(query), 250);
  }

  onColorFilterChange(): void {
    this.onProductSearchInput();
  }

  onWarehouseChange(): void {
    this.searchResults.set([]);
    if (this.productSearch.trim().length >= 2 || this.selectedColorId) {
      this.onProductSearchInput();
    }
  }

  onDocumentTypeChange(forceDefault = false): void {
    const availableFormats = this.getPrintFormatsForSelectedDocument();
    if (availableFormats.length === 0) {
      this.selectedPrintFormatId = this.defaultPrintFormat().id;
      return;
    }

    const currentAllowed = availableFormats.some((fmt) => fmt.id === this.selectedPrintFormatId);
    if (!currentAllowed || forceDefault) {
      const preferred = this.defaultPrintFormatIdForDocument(this.selectedDocumentCode());
      this.selectedPrintFormatId = availableFormats.some((fmt) => fmt.id === preferred)
        ? preferred
        : availableFormats[0].id;
    }
  }

  getPrintFormatsForSelectedDocument(): PosPrintFormatOption[] {
    const code = this.selectedDocumentCode();

    if (code === 'BOLETA') {
      return [
        { id: 'BOLETA_A4', label: 'Boleta A4', description: 'Formato hoja A4 para boleta.', mode: 'a4' },
        { id: 'BOLETA_T', label: 'Boleta T (Ticket 80 mm)', description: 'Formato térmico de 80 mm para boleta.', mode: 'ticket', widthMm: 80 },
        { id: 'BOLETA_TA', label: 'Boleta Ticket A (58 mm)', description: 'Formato térmico angosto de 58 mm para boleta.', mode: 'ticket', widthMm: 58 },
      ];
    }

    if (code === 'FACTURA') {
      return [
        { id: 'FACTURA_A4', label: 'Factura A4', description: 'Formato hoja A4 para factura.', mode: 'a4' },
        { id: 'FACTURA_T', label: 'Factura T (Ticket 80 mm)', description: 'Formato térmico de 80 mm para factura.', mode: 'ticket', widthMm: 80 },
      ];
    }

    if (code === 'NOTA_VENTA') {
      return [
        { id: 'NOTA_A4', label: 'Nota de venta A4', description: 'Formato hoja A4 para nota de venta.', mode: 'a4' },
        { id: 'NOTA_T', label: 'Nota de venta T (Ticket 80 mm)', description: 'Formato térmico de 80 mm para nota de venta.', mode: 'ticket', widthMm: 80 },
        { id: 'NOTA_TA', label: 'Nota de venta Ticket A (58 mm)', description: 'Formato térmico angosto de 58 mm para nota de venta.', mode: 'ticket', widthMm: 58 },
      ];
    }

    if (code === 'TICKET') {
      return [
        { id: 'TICKET_80', label: 'Ticket T (80 mm)', description: 'Ticket térmico estándar de 80 mm.', mode: 'ticket', widthMm: 80 },
        { id: 'TICKET_A', label: 'Ticket A (58 mm)', description: 'Ticket térmico angosto de 58 mm.', mode: 'ticket', widthMm: 58 },
      ];
    }

    return [this.defaultPrintFormat()];
  }

  selectedPrintFormatDescription(): string {
    return this.getSelectedPrintFormat().description;
  }

  private searchVariants(query: string): void {
    if (!this.selectedWarehouseId) {
      this.searchResults.set([]);
      return;
    }

    this.searching.set(true);
    const params: Record<string, string | number> = {
      warehouse_id: this.selectedWarehouseId,
      per_page: 120,
    };
    if (query) {
      params['search'] = query;
    }
    if (this.selectedColorId) {
      params['color_id'] = this.selectedColorId;
    }

    this.api.get<any>('stocks', {
      ...params,
    }).subscribe({
      next: (res) => {
        const rows = res?.data ?? (Array.isArray(res) ? res : []);
        const variants = rows
          .map((stock: any) => this.toPosVariant(stock))
          .filter((v: PosVariant | null): v is PosVariant => !!v)
          .sort((a: PosVariant, b: PosVariant) => a.product_name.localeCompare(b.product_name));

        this.searchResults.set(variants.slice(0, 40));
        this.searching.set(false);
      },
      error: () => {
        this.searchResults.set([]);
        this.searching.set(false);
      },
    });
  }

  private toPosVariant(stock: any): PosVariant | null {
    const product = stock?.product;
    if (!product || product.is_active === false) {
      return null;
    }

    const available = Number(stock?.available ?? (Number(stock?.quantity ?? 0) - Number(stock?.reserved ?? 0)));
    if (available <= 0) {
      return null;
    }

    const colorName = String(stock?.color?.name ?? '').trim();
    const size = String(stock?.size ?? '').trim();
    const variantLabel = [colorName, size].filter(Boolean).join(' · ');

    return {
      stock_id: Number(stock.id),
      product_id: Number(product.id),
      product_name: String(product.name ?? ''),
      sku: String(product.sku ?? ''),
      color_id: stock?.color_id ? Number(stock.color_id) : null,
      size: size || null,
      available_qty: available,
      unit_price: Number(product.base_price ?? 0),
      unit_cost: Number(product.unit_cost ?? 0),
      variant_label: variantLabel,
    };
  }

  addVariantToCart(variant: PosVariant): void {
    this.lines.update((ls) => {
      const next = [...ls];
      const existingIndex = next.findIndex((line) => line.stock_id === variant.stock_id);

      if (existingIndex >= 0) {
        const line = next[existingIndex];
        line.quantity = Math.min(line.available_qty, line.quantity + 1);
        line.subtotal = +(line.quantity * line.unit_price).toFixed(2);
        return next;
      }

      next.push({
        stock_id: variant.stock_id,
        product_id: variant.product_id,
        product_name: variant.product_name,
        variant_label: variant.variant_label,
        color_id: variant.color_id,
        size: variant.size,
        quantity: 1,
        unit_price: variant.unit_price,
        subtotal: +variant.unit_price.toFixed(2),
        available_qty: variant.available_qty,
        unit_cost: variant.unit_cost,
      });

      return next;
    });

    this.productSearch = '';
    this.searchResults.set([]);
  }

  onLineChange(index: number): void {
    this.lines.update((ls) => {
      const next = [...ls];
      const line = next[index];
      if (!line) {
        return ls;
      }

      line.quantity = Math.max(1, Number(line.quantity || 1));
      line.unit_price = Math.max(0, Number(line.unit_price || 0));

      if (line.quantity > line.available_qty) {
        line.quantity = line.available_qty;
      }

      line.subtotal = +(line.quantity * line.unit_price).toFixed(2);
      return next;
    });
  }

  removeLine(index: number): void {
    this.lines.update((ls) => ls.filter((_, i) => i !== index));
  }

  clearCart(): void {
    this.lines.set([]);
  }

  clearSearch(): void {
    this.productSearch = '';
    this.selectedColorId = null;
    this.searchResults.set([]);
    this.searching.set(false);
  }

  requiresCustomerData(): boolean {
    return this.includeCustomerData;
  }

  saveSale(): void {
    this.error.set('');
    this.success.set('');
    this.info.set('');

    if (!this.selectedWarehouseId) {
      this.error.set('No hay almacén configurado para POS. Activa uno en Configuración > Almacenes.');
      return;
    }

    if (!this.selectedPaymentMethodId) {
      this.error.set('Selecciona el método de pago.');
      return;
    }

    const requiresCustomerData = this.requiresCustomerData();
    if (requiresCustomerData && !this.customerName.trim()) {
      this.error.set('Ingresa el nombre del cliente para completar la venta.');
      return;
    }

    if (requiresCustomerData && !this.customerDni.trim()) {
      this.error.set('Ingresa DNI/RUC del cliente para completar la venta.');
      return;
    }

    const validLines = this.lines()
      .filter((line) => line.product_id > 0 && line.quantity > 0)
      .map((line) => ({
        product_id: line.product_id,
        color_id: line.color_id,
        size: line.size,
        product_description: [line.product_name, line.variant_label].filter(Boolean).join(' · '),
        quantity: line.quantity,
        unit_price: +line.unit_price,
        subtotal: +line.subtotal,
      }));

    if (validLines.length === 0) {
      this.error.set('Agrega al menos un producto para registrar la venta.');
      return;
    }

    const paymentMethod = this.paymentMethods().find((pm) => pm.id === this.selectedPaymentMethodId);
    const ticketLines = this.lines().map((line) => ({
      product_name: line.product_name,
      variant_label: line.variant_label,
      quantity: line.quantity,
      unit_price: line.unit_price,
      subtotal: line.subtotal,
    }));

    const observations = [
      paymentMethod ? `POS · Método de pago: ${paymentMethod.name}` : 'POS',
      this.note?.trim() ? this.note.trim() : null,
    ].filter(Boolean).join(' | ');

    const selectedPrintFormat = this.getSelectedPrintFormat();

    let printWindow: Window | null = null;
    if (this.printAfterSave) {
      printWindow = this.openPrintWindow(selectedPrintFormat);
    }

    this.saving.set(true);

    this.api.post<any>('orders', {
      is_pos: true,
      order_date: new Date().toISOString(),
      warehouse_id: this.selectedWarehouseId,
      shipping_agency_id: null,
      purchase_type_id: null,
      observations,
      phone: requiresCustomerData ? (this.customerPhone || null) : null,
      customer_id: null,
      customer_name: requiresCustomerData ? (this.customerName || null) : null,
      dni: requiresCustomerData ? (this.customerDni || null) : null,
      province_id: null,
      district_id: null,
      address: requiresCustomerData ? (this.customerAddress || null) : null,
      delivery_cost: 0,
      total: this.total(),
      document_type_id: this.selectedDocumentTypeId,
      customer_email: null,
      needs_receipt: false,
      items: validLines,
    }).subscribe({
      next: (created) => {
        this.saving.set(false);

        const orderNumber = String(created?.order_number ?? `#${created?.id ?? '-'}`);
        if (printWindow && this.printAfterSave) {
          this.renderPrintDocument(printWindow, {
            orderNumber,
            paymentMethod: paymentMethod?.name ?? 'No especificado',
            documentType: this.documentTypes().find((d) => d.id === this.selectedDocumentTypeId)?.name ?? 'Ticket',
            total: this.total(),
            lines: ticketLines,
            customerName: this.customerName || null,
            customerDoc: this.customerDni || null,
            printFormat: selectedPrintFormat,
            storeName: this.selectedWarehouseName(),
          });
        }

        this.resetAfterSale();
        this.success.set(`Venta registrada correctamente (${orderNumber}).`);
      },
      error: (e) => {
        this.saving.set(false);
        const msg = e?.error?.message ?? e?.error?.errors ?? 'No se pudo registrar la venta POS.';
        this.error.set(typeof msg === 'string' ? msg : JSON.stringify(msg));
        if (printWindow) {
          printWindow.close();
        }
      },
    });
  }

  printCurrentTicketTest(): void {
    this.error.set('');
    this.info.set('');

    if (this.lines().length === 0) {
      this.error.set('Agrega al menos un producto para probar la impresión de ticket.');
      return;
    }

    const paymentMethod = this.paymentMethods().find((pm) => pm.id === this.selectedPaymentMethodId);
    const selectedPrintFormat = this.getSelectedPrintFormat();
    const printWindow = this.openPrintWindow(selectedPrintFormat);
    if (!printWindow) {
      this.error.set('El navegador bloqueó la ventana de impresión. Permite popups para continuar.');
      return;
    }

    this.renderPrintDocument(printWindow, {
      orderNumber: 'PRUEBA-TICKET',
      paymentMethod: paymentMethod?.name ?? 'No especificado',
      documentType: this.documentTypes().find((d) => d.id === this.selectedDocumentTypeId)?.name ?? 'Ticket',
      total: this.total(),
      lines: this.lines().map((line) => ({
        product_name: line.product_name,
        variant_label: line.variant_label,
        quantity: line.quantity,
        unit_price: line.unit_price,
        subtotal: line.subtotal,
      })),
      customerName: this.customerName || null,
      customerDoc: this.customerDni || null,
      printFormat: selectedPrintFormat,
      storeName: this.selectedWarehouseName(),
    });

    this.info.set(`Se abrió una prueba de impresión en formato ${selectedPrintFormat.label}.`);
  }

  async testBluetoothPrinter(): Promise<void> {
    this.error.set('');
    this.info.set('');

    const nav = navigator as Navigator & { bluetooth?: { requestDevice: (opts: any) => Promise<any> } };
    if (!nav.bluetooth?.requestDevice) {
      this.error.set('Tu navegador no soporta Web Bluetooth. Usa Chrome/Edge en dispositivo compatible.');
      return;
    }

    this.btTesting.set(true);
    try {
      const device = await nav.bluetooth.requestDevice({
        acceptAllDevices: true,
        optionalServices: ['battery_service'],
      });

      this.info.set(`Dispositivo detectado: ${device?.name ?? 'Sin nombre'} (${device?.id ?? 'sin id'}).`);
      if (device?.gatt?.connected) {
        device.gatt.disconnect();
      }
    } catch (err: any) {
      if (err?.name === 'NotFoundError') {
        this.info.set('Prueba Bluetooth cancelada.');
      } else {
        this.error.set('No se pudo completar la prueba de impresora Bluetooth.');
      }
    } finally {
      this.btTesting.set(false);
    }
  }

  private resetAfterSale(): void {
    this.lines.set([]);
    this.productSearch = '';
    this.searchResults.set([]);

    this.includeCustomerData = false;

    this.customerName = '';
    this.customerDni = '';
    this.customerPhone = '';
    this.customerAddress = '';
    this.note = '';
  }

  private renderPrintDocument(printWindow: Window, data: PosPrintPayload): void {
    if (data.printFormat.mode === 'a4') {
      this.renderA4Document(printWindow, data);
      return;
    }

    this.renderTicket(printWindow, data, data.printFormat.widthMm ?? 80);
  }

  private renderTicket(printWindow: Window, data: PosPrintPayload, widthMm: number): void {
    const now = new Date();
    const ticketWidth = widthMm <= 58 ? 58 : 80;

    const linesHtml = data.lines.map((line) => {
      const detail = [line.product_name, line.variant_label].filter(Boolean).join(' · ');
      return `
        <tr>
          <td>${this.escapeHtml(detail)}</td>
          <td class="num">${line.quantity}</td>
          <td class="num">${line.unit_price.toFixed(2)}</td>
          <td class="num">${line.subtotal.toFixed(2)}</td>
        </tr>
      `;
    }).join('');

    const html = `
      <!doctype html>
      <html>
      <head>
        <meta charset="utf-8" />
        <title>Ticket ${this.escapeHtml(data.orderNumber)}</title>
        <style>
          @page { size: ${ticketWidth}mm auto; margin: 4mm; }
          body { font-family: Arial, sans-serif; margin: 0; padding: 0; font-size: 12px; }
          .ticket { width: ${ticketWidth}mm; margin: 0 auto; padding: 6px; }
          h2 { margin: 0 0 4px; font-size: 16px; text-align: center; }
          .muted { color: #666; font-size: 11px; text-align: center; margin-bottom: 8px; }
          .meta { margin: 6px 0; font-size: 11px; }
          table { width: 100%; border-collapse: collapse; font-size: 11px; }
          th, td { border-bottom: 1px dashed #ccc; padding: 4px 0; vertical-align: top; }
          .num { text-align: right; white-space: nowrap; }
          .total { margin-top: 8px; font-size: 14px; font-weight: bold; text-align: right; }
          .foot { margin-top: 10px; text-align: center; font-size: 10px; color: #666; }
        </style>
      </head>
      <body>
        <div class="ticket">
          <h2>HIITOP</h2>
          <div class="muted">${this.escapeHtml(data.printFormat.label)} · ${ticketWidth}mm</div>
          <div class="meta">Ticket: ${this.escapeHtml(data.orderNumber)}</div>
          <div class="meta">Fecha: ${now.toLocaleString()}</div>
          <div class="meta">Tienda: ${this.escapeHtml(data.storeName)}</div>
          <div class="meta">Documento: ${this.escapeHtml(data.documentType)}</div>
          <div class="meta">Pago: ${this.escapeHtml(data.paymentMethod)}</div>
          ${data.customerName ? `<div class="meta">Cliente: ${this.escapeHtml(data.customerName)}</div>` : ''}
          ${data.customerDoc ? `<div class="meta">Doc: ${this.escapeHtml(data.customerDoc)}</div>` : ''}

          <table>
            <thead>
              <tr>
                <th>Producto</th>
                <th class="num">Cant.</th>
                <th class="num">P/U</th>
                <th class="num">Sub</th>
              </tr>
            </thead>
            <tbody>${linesHtml}</tbody>
          </table>

          <div class="total">TOTAL: S/ ${data.total.toFixed(2)}</div>
          <div class="foot">Gracias por tu compra</div>
        </div>
      </body>
      </html>
    `;

    printWindow.document.open();
    printWindow.document.write(html);
    printWindow.document.close();

    setTimeout(() => {
      printWindow.focus();
      printWindow.print();
      printWindow.close();
    }, 250);
  }

  private renderA4Document(printWindow: Window, data: PosPrintPayload): void {
    const now = new Date();

    const rowsHtml = data.lines.map((line, index) => {
      const detail = [line.product_name, line.variant_label].filter(Boolean).join(' · ');
      return `
        <tr>
          <td class="num">${index + 1}</td>
          <td>${this.escapeHtml(detail)}</td>
          <td class="num">${line.quantity}</td>
          <td class="num">${line.unit_price.toFixed(2)}</td>
          <td class="num">${line.subtotal.toFixed(2)}</td>
        </tr>
      `;
    }).join('');

    const html = `
      <!doctype html>
      <html>
      <head>
        <meta charset="utf-8" />
        <title>${this.escapeHtml(data.documentType)} ${this.escapeHtml(data.orderNumber)}</title>
        <style>
          @page { size: A4 portrait; margin: 12mm; }
          body { font-family: Arial, sans-serif; margin: 0; color: #0f172a; font-size: 12px; }
          .sheet { width: 100%; max-width: 186mm; margin: 0 auto; }
          .header { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; margin-bottom: 12px; }
          .brand h1 { margin: 0 0 4px; font-size: 20px; }
          .muted { color: #475569; font-size: 11px; }
          .doc-box { border: 1px solid #cbd5e1; border-radius: 8px; padding: 8px 10px; text-align: right; min-width: 220px; }
          .doc-box .title { font-size: 13px; font-weight: 700; margin-bottom: 2px; }
          .meta-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8px 14px; margin-bottom: 12px; }
          .meta-item { border: 1px solid #e2e8f0; border-radius: 6px; padding: 6px 8px; }
          .meta-item strong { display: block; font-size: 11px; color: #475569; margin-bottom: 2px; }
          table { width: 100%; border-collapse: collapse; }
          th, td { border: 1px solid #dbe1e8; padding: 6px 8px; vertical-align: top; }
          th { background: #f8fafc; text-align: left; }
          .num { text-align: right; white-space: nowrap; }
          .totals { margin-top: 12px; display: flex; justify-content: flex-end; }
          .totals-box { width: 280px; border: 1px solid #dbe1e8; border-radius: 8px; padding: 8px 10px; }
          .totals-row { display: flex; justify-content: space-between; margin-bottom: 6px; }
          .totals-row:last-child { margin-bottom: 0; font-size: 14px; font-weight: 700; }
          .foot { margin-top: 16px; text-align: center; font-size: 11px; color: #64748b; }
        </style>
      </head>
      <body>
        <div class="sheet">
          <div class="header">
            <div class="brand">
              <h1>HIITOP</h1>
              <div class="muted">Comprobante de tienda - Formato A4</div>
              <div class="muted">Tienda: ${this.escapeHtml(data.storeName)}</div>
            </div>
            <div class="doc-box">
              <div class="title">${this.escapeHtml(data.documentType)}</div>
              <div>N° ${this.escapeHtml(data.orderNumber)}</div>
              <div class="muted">${this.escapeHtml(data.printFormat.label)}</div>
            </div>
          </div>

          <div class="meta-grid">
            <div class="meta-item"><strong>Fecha</strong>${now.toLocaleString()}</div>
            <div class="meta-item"><strong>Método de pago</strong>${this.escapeHtml(data.paymentMethod)}</div>
            <div class="meta-item"><strong>Cliente</strong>${this.escapeHtml(data.customerName || 'Público general')}</div>
            <div class="meta-item"><strong>Documento</strong>${this.escapeHtml(data.customerDoc || '-')}</div>
          </div>

          <table>
            <thead>
              <tr>
                <th class="num">#</th>
                <th>Descripción</th>
                <th class="num">Cant.</th>
                <th class="num">P/U</th>
                <th class="num">Subtotal</th>
              </tr>
            </thead>
            <tbody>${rowsHtml}</tbody>
          </table>

          <div class="totals">
            <div class="totals-box">
              <div class="totals-row"><span>Total</span><span>S/ ${data.total.toFixed(2)}</span></div>
            </div>
          </div>

          <div class="foot">Gracias por tu compra</div>
        </div>
      </body>
      </html>
    `;

    printWindow.document.open();
    printWindow.document.write(html);
    printWindow.document.close();

    setTimeout(() => {
      printWindow.focus();
      printWindow.print();
      printWindow.close();
    }, 250);
  }

  private selectedDocumentCode(): string {
    const docCode = this.documentTypes().find((d) => d.id === this.selectedDocumentTypeId)?.code;
    return String(docCode ?? '').toUpperCase();
  }

  private defaultPrintFormatIdForDocument(documentCode: string): string {
    switch (documentCode) {
      case 'BOLETA':
        return 'BOLETA_T';
      case 'FACTURA':
        return 'FACTURA_A4';
      case 'NOTA_VENTA':
        return 'NOTA_T';
      case 'TICKET':
        return 'TICKET_80';
      default:
        return this.defaultPrintFormat().id;
    }
  }

  private defaultPrintFormat(): PosPrintFormatOption {
    return {
      id: 'TICKET_80',
      label: 'Ticket T (80 mm)',
      description: 'Ticket térmico estándar de 80 mm.',
      mode: 'ticket',
      widthMm: 80,
    };
  }

  private getSelectedPrintFormat(): PosPrintFormatOption {
    const formats = this.getPrintFormatsForSelectedDocument();
    return formats.find((fmt) => fmt.id === this.selectedPrintFormatId)
      ?? formats[0]
      ?? this.defaultPrintFormat();
  }

  private openPrintWindow(format: PosPrintFormatOption): Window | null {
    const specs = format.mode === 'a4'
      ? 'width=980,height=760'
      : 'width=420,height=740';
    return window.open('', '_blank', specs);
  }

  private escapeHtml(value: string): string {
    const map: Record<string, string> = {
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#039;',
    };

    return String(value).replace(/[&<>"']/g, (char) => map[char]);
  }
}
