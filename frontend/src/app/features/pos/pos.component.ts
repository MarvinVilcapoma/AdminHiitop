import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ProductLookupComponent } from '../../core/components';
import {
  Color,
  DocumentPrintFormat,
  DocumentType,
  PaymentMethod,
  PosInitialData,
  ProductLookupItem,
  Warehouse,
} from '../../core/models';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';

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

interface PosPrintFormatOption {
  id: number;
  code: string;
  label: string;
  description: string;
  mode: 'a4' | 'ticket' | 'pdf';
  widthMm?: number;
  isDefault?: boolean;
}

interface PosPrintPayload {
  orderNumber: string;
  paymentMethod: string;
  documentType: string;
  subtotal: number;
  discountAmount: number;
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
  imports: [FormsModule, DecimalPipe, RouterLink, ProductLookupComponent],
  templateUrl: './pos.component.html',
  styleUrl: './pos.component.scss',
})
export class PosComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);

  loading = signal(false);
  saving = signal(false);
  btTesting = signal(false);

  error = signal('');
  info = signal('');

  warehouses = signal<Warehouse[]>([]);
  paymentMethods = signal<PaymentMethod[]>([]);
  documentTypes = signal<DocumentType[]>([]);
  colors = signal<Color[]>([]);

  selectedWarehouseId: number | null = null;
  selectedPaymentMethodId: number | null = null;
  selectedDocumentTypeId: number | null = null;
  selectedColorId: number | null = null;
  selectedPrintFormatId: number | null = null;

  includeCustomerData = false;
  printAfterSave = true;
  lookupResetKey = 0;
  lookupRefreshKey = 0;

  customerName = '';
  customerDni = '';
  customerPhone = '';
  customerAddress = '';
  note = '';

  lines = signal<PosLine[]>([]);

  selectedWarehouseName = computed(() =>
    this.warehouses().find((warehouse) => warehouse.id === this.selectedWarehouseId)?.name ?? 'Sin almacen activo'
  );

  hasCartLines = computed(() => this.lines().length > 0);

  selectedDocumentType = computed(() =>
    this.documentTypes().find((document) => document.id === this.selectedDocumentTypeId) ?? null
  );

  selectedDocumentTypeName = computed(() => this.selectedDocumentType()?.name ?? 'Sin documento');
  selectedPaymentMethodName = computed(() =>
    this.paymentMethods().find((method) => method.id === this.selectedPaymentMethodId)?.name ?? 'Sin metodo'
  );
  selectedPrintFormatName = computed(() => this.getSelectedPrintFormat()?.label ?? 'Sin formato');

  discountType = signal<'percent' | 'fixed'>('percent');
  discountValue = signal(0);

  lineCount = computed(() => this.lines().reduce((sum, line) => sum + line.quantity, 0));
  total = computed(() => +(this.lines().reduce((sum, line) => sum + line.subtotal, 0)).toFixed(2));
  totalCost = computed(() => +(this.lines().reduce((sum, line) => sum + (line.quantity * line.unit_cost), 0)).toFixed(2));
  totalProfit = computed(() => +(this.finalTotal() - this.totalCost()).toFixed(2));

  discountAmount = computed(() => {
    const v = Number(this.discountValue()) || 0;
    if (v <= 0 || !this.hasCartLines()) return 0;
    const sub = this.total();
    if (this.discountType() === 'percent') return +(sub * Math.min(v, 100) / 100).toFixed(2);
    return +Math.min(v, sub).toFixed(2);
  });

  finalTotal = computed(() => +(this.total() - this.discountAmount()).toFixed(2));

  ngOnInit(): void {
    this.loadCatalogs();
  }

  loadCatalogs(): void {
    this.loading.set(true);

    this.api.get<PosInitialData>('pos/initial-data').subscribe({
      next: (payload) => {
        const warehouseRows = Array.isArray(payload?.warehouses) ? payload.warehouses : [];
        const documentRows = Array.isArray(payload?.document_types) ? payload.document_types : [];
        const paymentRows = Array.isArray(payload?.payment_methods) ? payload.payment_methods : [];
        const colorRows = Array.isArray(payload?.colors) ? payload.colors : [];
        const settings = payload?.settings ?? {};

        this.warehouses.set(warehouseRows);
        this.documentTypes.set(documentRows);
        this.paymentMethods.set(paymentRows);
        this.colors.set(colorRows);

        this.selectedWarehouseId = this.resolveDefaultWarehouseId(warehouseRows, settings);

        const boleta = documentRows.find((doc) => String(doc.code ?? '').toUpperCase() === 'BOLETA');
        const factura = documentRows.find((doc) => String(doc.code ?? '').toUpperCase() === 'FACTURA');
        const orderSale = documentRows.find((doc) => String(doc.code ?? '').toUpperCase() === 'ORDEN_VENTA');
        const quote = documentRows.find((doc) => String(doc.code ?? '').toUpperCase() === 'COTIZACION');

        this.selectedDocumentTypeId =
          boleta?.id ?? factura?.id ?? orderSale?.id ?? quote?.id ?? documentRows[0]?.id ?? null;

        this.onDocumentTypeChange(true);
        this.selectedPaymentMethodId = paymentRows[0]?.id ?? null;

        if (!this.selectedWarehouseId) {
          this.error.set('No hay almacenes activos marcados como punto de venta. Configuralo en Ajustes > Almacenes.');
        }

        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('No se pudo cargar la configuracion inicial del POS.');
      },
    });
  }

  private resolveDefaultWarehouseId(rows: Warehouse[], settings: PosInitialData['settings']): number | null {
    const configuredId = Number(settings?.['pos_default_warehouse_id']?.value ?? 0);
    if (configuredId > 0 && rows.some((warehouse) => warehouse.id === configuredId)) {
      return configuredId;
    }

    const firstStore = rows.find((warehouse) => {
      const directType = String((warehouse as any).type ?? '').toLowerCase();
      const typeCode = String(warehouse.warehouse_type?.code ?? '').toLowerCase();
      const typeName = String(warehouse.warehouse_type?.name ?? '').toLowerCase();

      return (
        directType === 'store' ||
        typeCode.includes('store') ||
        typeCode.includes('tienda') ||
        typeName.includes('tienda')
      );
    });

    return firstStore?.id ?? rows[0]?.id ?? null;
  }

  onColorFilterChange(): void {
    this.error.set('');
    this.info.set('');
    this.lookupRefreshKey += 1;
  }

  onWarehouseChange(): void {
    this.error.set('');
    this.info.set('');
    this.lookupRefreshKey += 1;
  }

  handleLookupError(message: string): void {
    this.error.set(message);
  }

  handleLookupQueryChange(query: string): void {
    if (!query.trim()) {
      this.info.set('');
      this.error.set('');
    }
  }

  onDocumentTypeChange(forceDefault = false): void {
    const availableFormats = this.getPrintFormatsForSelectedDocument();
    if (availableFormats.length === 0) {
      this.selectedPrintFormatId = null;
      return;
    }

    const currentAllowed = availableFormats.some((format) => format.id === this.selectedPrintFormatId);
    if (!currentAllowed || forceDefault) {
      const preferred = availableFormats.find((format) => format.isDefault) ?? availableFormats[0];
      this.selectedPrintFormatId = preferred?.id ?? null;
    }

    if (this.selectedDocumentType()?.requires_customer) {
      this.includeCustomerData = true;
    }
  }

  getPrintFormatsForSelectedDocument(): PosPrintFormatOption[] {
    const documentType = this.selectedDocumentType();
    const rawFormats = (documentType as any)?.print_formats ?? documentType?.printFormats ?? [];
    const formats = Array.isArray(rawFormats) ? rawFormats : [];

    return formats.map((format) => this.toPrintFormatOption(format));
  }

  private toPrintFormatOption(format: DocumentPrintFormat): PosPrintFormatOption {
    const code = String(format.code ?? '').toUpperCase();
    const mode = (format.mode ?? this.inferPrintMode(code)) as 'a4' | 'ticket' | 'pdf';
    const widthMm = format.width_mm ?? (code === 'TICKET' ? 80 : undefined);

    return {
      id: format.id,
      code,
      label: format.name ?? code,
      description: this.describePrintFormat(code, mode, widthMm),
      mode,
      widthMm: widthMm ?? undefined,
      isDefault: format.pivot?.is_default === true,
    };
  }

  private inferPrintMode(code: string): 'a4' | 'ticket' | 'pdf' {
    if (code === 'A4') {
      return 'a4';
    }

    if (code === 'PDF') {
      return 'pdf';
    }

    return 'ticket';
  }

  private describePrintFormat(code: string, mode: 'a4' | 'ticket' | 'pdf', widthMm?: number): string {
    if (mode === 'ticket') {
      return `Formato termico ${widthMm ?? 80} mm para impresion rapida en caja.`;
    }

    if (mode === 'pdf') {
      return 'Representacion en PDF para descarga o envio digital.';
    }

    return code === 'A4'
      ? 'Formato hoja A4 para impresion completa.'
      : 'Formato de impresion estructurado.';
  }

  addVariantToCart(variant: ProductLookupItem): void {
    if (!variant.stock_id) {
      this.error.set('La variante seleccionada no tiene stock asociado.');
      return;
    }

    this.lines.update((rows) => {
      const next = [...rows];
      const existingIndex = next.findIndex((line) => line.stock_id === variant.stock_id);

      if (existingIndex >= 0) {
        const line = next[existingIndex];
        line.quantity = Math.min(line.available_qty, line.quantity + 1);
        line.subtotal = +(line.quantity * line.unit_price).toFixed(2);
        return next;
      }

      const stockId = variant.stock_id as number;

      next.push({
        stock_id: stockId,
        product_id: variant.product_id,
        product_name: variant.product_name,
        variant_label: variant.variant_label ?? '',
        color_id: variant.color_id ?? null,
        size: variant.size ?? null,
        quantity: 1,
        unit_price: Number(variant.unit_price ?? 0),
        subtotal: +Number(variant.unit_price ?? 0).toFixed(2),
        available_qty: Number(variant.available_qty ?? 0),
        unit_cost: Number(variant.unit_cost ?? 0),
      });

      return next;
    });
  }

  cancelSale(): void {
    this.clearCart();
    this.lookupResetKey += 1;
    this.includeCustomerData = this.selectedDocumentType()?.requires_customer === true;
    this.customerName = '';
    this.customerDni = '';
    this.customerPhone = '';
    this.customerAddress = '';
    this.note = '';
    this.info.set('Venta reiniciada.');
  }

  saveDraft(): void {
    this.info.set(
      'La opcion de borrador quedara enlazada al flujo comercial en la siguiente fase. Por ahora puedes seguir registrando la venta normal.'
    );
  }

  onLineChange(index: number): void {
    this.lines.update((rows) => {
      const next = [...rows];
      const line = next[index];

      if (!line) {
        return rows;
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
    this.lines.update((rows) => rows.filter((_, currentIndex) => currentIndex !== index));
  }

  clearCart(): void {
    this.lines.set([]);
    this.discountValue.set(0);
  }

  clearSearch(): void {
    this.selectedColorId = null;
    this.lookupResetKey += 1;
    this.info.set('');
  }

  requiresCustomerData(): boolean {
    return this.selectedDocumentType()?.requires_customer === true || this.includeCustomerData;
  }

  saveSale(): void {
    this.error.set('');
    this.info.set('');

    if (!this.selectedWarehouseId) {
      this.error.set('No hay almacen configurado para POS. Activa uno en Configuracion > Almacenes.');
      return;
    }

    if (!this.selectedPaymentMethodId) {
      this.error.set('Selecciona el metodo de pago.');
      return;
    }

    if (!this.selectedDocumentTypeId) {
      this.error.set('Selecciona el tipo de documento.');
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

    const paymentMethod = this.paymentMethods().find((method) => method.id === this.selectedPaymentMethodId);
    const ticketLines = this.lines().map((line) => ({
      product_name: line.product_name,
      variant_label: line.variant_label,
      quantity: line.quantity,
      unit_price: line.unit_price,
      subtotal: line.subtotal,
    }));

    const observations = [
      paymentMethod ? `POS · Metodo de pago: ${paymentMethod.name}` : 'POS',
      this.note?.trim() ? this.note.trim() : null,
    ]
      .filter(Boolean)
      .join(' | ');

    const selectedPrintFormat = this.getSelectedPrintFormat();
    let printWindow: Window | null = null;

    if (this.printAfterSave && selectedPrintFormat) {
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
      discount_type: this.discountAmount() > 0 ? this.discountType() : null,
      discount_value: this.discountAmount() > 0 ? this.discountValue() : null,
      discount_amount: this.discountAmount(),
      total: this.finalTotal(),
      document_type_id: this.selectedDocumentTypeId,
      document_print_format_id: this.selectedPrintFormatId,
      customer_email: null,
      needs_receipt: false,
      items: validLines,
    }).subscribe({
      next: (created) => {
        this.saving.set(false);

        const orderNumber = String(created?.order_number ?? `#${created?.id ?? '-'}`);
        if (printWindow && this.printAfterSave && selectedPrintFormat) {
          this.renderPrintDocument(printWindow, {
            orderNumber,
            paymentMethod: paymentMethod?.name ?? 'No especificado',
            documentType: this.selectedDocumentType()?.name ?? 'Documento',
            subtotal: this.total(),
            discountAmount: this.discountAmount(),
            total: this.finalTotal(),
            lines: ticketLines,
            customerName: this.customerName || null,
            customerDoc: this.customerDni || null,
            printFormat: selectedPrintFormat,
            storeName: this.selectedWarehouseName(),
          });
        }

        this.resetAfterSale();
        this.toast.success(`Venta registrada correctamente (${orderNumber}).`);
      },
      error: (errorResponse) => {
        this.saving.set(false);
        const message = errorResponse?.error?.message ?? errorResponse?.error?.errors ?? 'No se pudo registrar la venta POS.';
        this.error.set(typeof message === 'string' ? message : JSON.stringify(message));
        if (printWindow) {
          printWindow.close();
        }
      },
    });
  }

  printCurrentTicketTest(): void {
    this.error.set('');
    this.info.set('');

    const selectedPrintFormat = this.getSelectedPrintFormat();
    if (!selectedPrintFormat) {
      this.error.set('No hay formato de impresion disponible para este documento.');
      return;
    }

    if (this.lines().length === 0) {
      this.error.set('Agrega al menos un producto para probar la impresion.');
      return;
    }

    const paymentMethod = this.paymentMethods().find((method) => method.id === this.selectedPaymentMethodId);
    const printWindow = this.openPrintWindow(selectedPrintFormat);
    if (!printWindow) {
      this.error.set('El navegador bloqueo la ventana de impresion. Permite popups para continuar.');
      return;
    }

    this.renderPrintDocument(printWindow, {
      orderNumber: 'PRUEBA-POS',
      paymentMethod: paymentMethod?.name ?? 'No especificado',
      documentType: this.selectedDocumentType()?.name ?? 'Documento',
      subtotal: this.total(),
      discountAmount: this.discountAmount(),
      total: this.finalTotal(),
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

    this.info.set(`Se abrio una prueba de impresion en formato ${selectedPrintFormat.label}.`);
  }

  async testBluetoothPrinter(): Promise<void> {
    this.error.set('');
    this.info.set('');

    const nav = navigator as Navigator & { bluetooth?: { requestDevice: (options: any) => Promise<any> } };
    if (!nav.bluetooth?.requestDevice) {
      this.error.set('Tu navegador no soporta Web Bluetooth. Usa Chrome o Edge en un dispositivo compatible.');
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
    } catch (error: any) {
      if (error?.name === 'NotFoundError') {
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
    this.discountValue.set(0);
    this.lookupResetKey += 1;

    if (this.selectedDocumentType()?.requires_customer !== true) {
      this.includeCustomerData = false;
    }

    this.customerName = '';
    this.customerDni = '';
    this.customerPhone = '';
    this.customerAddress = '';
    this.note = '';
  }

  private renderPrintDocument(printWindow: Window, data: PosPrintPayload): void {
    if (data.printFormat.mode === 'a4' || data.printFormat.mode === 'pdf') {
      this.renderA4Document(printWindow, data);
      return;
    }

    this.renderTicket(printWindow, data, data.printFormat.widthMm ?? 80);
  }

  private renderTicket(printWindow: Window, data: PosPrintPayload, widthMm: number): void {
    const now = new Date();
    const ticketWidth = widthMm <= 58 ? 58 : 80;

    const linesHtml = data.lines
      .map((line) => {
        const detail = [line.product_name, line.variant_label].filter(Boolean).join(' · ');
        return `
        <tr>
          <td>${this.escapeHtml(detail)}</td>
          <td class="num">${line.quantity}</td>
          <td class="num">${line.unit_price.toFixed(2)}</td>
          <td class="num">${line.subtotal.toFixed(2)}</td>
        </tr>
      `;
      })
      .join('');

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
          .discount { margin-top: 4px; font-size: 12px; text-align: right; color: #e00; }
          .foot { margin-top: 10px; text-align: center; font-size: 10px; color: #666; }
        </style>
      </head>
      <body>
        <div class="ticket">
          <h2>HIITOP</h2>
          <div class="muted">${this.escapeHtml(data.printFormat.label)} · ${ticketWidth}mm</div>
          <div class="meta">Documento: ${this.escapeHtml(data.documentType)}</div>
          <div class="meta">Numero: ${this.escapeHtml(data.orderNumber)}</div>
          <div class="meta">Fecha: ${now.toLocaleString()}</div>
          <div class="meta">Tienda: ${this.escapeHtml(data.storeName)}</div>
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

          ${data.discountAmount > 0 ? `<div class="discount">Descuento: -S/ ${data.discountAmount.toFixed(2)}</div>` : ''}
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

    const rowsHtml = data.lines
      .map((line, index) => {
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
      })
      .join('');

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
              <div class="muted">Representacion comercial - ${this.escapeHtml(data.printFormat.label)}</div>
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
            <div class="meta-item"><strong>Metodo de pago</strong>${this.escapeHtml(data.paymentMethod)}</div>
            <div class="meta-item"><strong>Cliente</strong>${this.escapeHtml(data.customerName || 'Publico general')}</div>
            <div class="meta-item"><strong>Documento</strong>${this.escapeHtml(data.customerDoc || '-')}</div>
          </div>

          <table>
            <thead>
              <tr>
                <th class="num">#</th>
                <th>Descripcion</th>
                <th class="num">Cant.</th>
                <th class="num">P/U</th>
                <th class="num">Subtotal</th>
              </tr>
            </thead>
            <tbody>${rowsHtml}</tbody>
          </table>

          <div class="totals">
            <div class="totals-box">
              ${data.discountAmount > 0 ? `<div class="totals-row"><span>Subtotal</span><span>S/ ${data.subtotal.toFixed(2)}</span></div><div class="totals-row" style="color:#c00"><span>Descuento</span><span>-S/ ${data.discountAmount.toFixed(2)}</span></div>` : ''}
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

  private getSelectedPrintFormat(): PosPrintFormatOption | null {
    const formats = this.getPrintFormatsForSelectedDocument();
    return formats.find((format) => format.id === this.selectedPrintFormatId) ?? formats[0] ?? null;
  }

  private openPrintWindow(format: PosPrintFormatOption): Window | null {
    const specs = format.mode === 'a4' || format.mode === 'pdf'
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
      '\'': '&#039;',
    };

    return String(value).replace(/[&<>"']/g, (char) => map[char]);
  }
}
