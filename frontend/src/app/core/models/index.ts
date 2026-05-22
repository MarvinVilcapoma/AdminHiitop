/**
 * Shared domain models — single source of truth for all API shapes.
 * Import from here in all components/services instead of declaring inline.
 */

// ── Auth / Users ─────────────────────────────────────────────────────────────

export interface Role {
  id: number;
  name: string;
}

export interface AppUser {
  id: number;
  name: string;
  email: string;
  is_active?: boolean;
  roles?: Role[];
  created_at?: string;
}

// ── Catalog / Lookups ────────────────────────────────────────────────────────

export interface IdName {
  id: number;
  name: string;
}

export interface OrderStatus extends IdName {
  color?: string;
  slug?: string;
  is_protected?: boolean;
}

export interface Size {
  id: number;
  name: string;
  sort_order?: number;
}

export interface Setting {
  value: unknown;
  raw?: string;
  label?: string;
  type?: string;
  group?: string;
}

export interface ShippingAgency extends IdName {
  code?: string;
  shipping_rate?: number;
  is_active?: boolean;
}
export interface PurchaseType   extends IdName {}
export interface DocumentPrintFormat extends IdName {
  code?: string;
  mode?: 'a4' | 'ticket' | 'pdf';
  width_mm?: number | null;
  is_active?: boolean;
  pivot?: { is_default?: boolean };
}

export interface DocumentType extends IdName {
  code?: string;
  is_active?: boolean;
  is_protected?: boolean;
  is_sunat_document?: boolean;
  requires_customer?: boolean;
  requires_related_document?: boolean;
  can_be_converted?: boolean;
  is_commercial_document?: boolean;
  sort_order?: number;
  printFormats?: DocumentPrintFormat[];
  print_formats?: DocumentPrintFormat[];
}
export interface ProductType    extends IdName { slug?: string; is_active?: boolean; sizes?: Size[]; }
export interface Collection     extends IdName { slug?: string; description?: string; is_active?: boolean; }
export interface Province       extends IdName { code?: string; is_active?: boolean; districts?: District[]; }
export interface District       extends IdName { code?: string; is_active?: boolean; province_id?: number; province?: Province; }
export interface Color          extends IdName { hex_code?: string; is_active?: boolean; }
export interface WarehouseType  extends IdName { code?: string; description?: string; is_active?: boolean; }
export interface PaymentMethod  extends IdName { code?: string; is_active?: boolean; }

// ── Customer ─────────────────────────────────────────────────────────────────

export interface Customer {
  id: number;
  full_name: string;
  dni?: string;
  phone?: string;
  email?: string;
  address?: string;
  province_id?: number;
  district_id?: number;
  province?: Province;
  district?: District;
  is_active?: boolean;
  // Fiscal fields
  document_type?: string;   // DNI, RUC, CE, PAS
  ruc?: string;
  razon_social?: string;
  nombre_comercial?: string;
  created_at?: string;
}

// ── Product / Stock ──────────────────────────────────────────────────────────

export interface Product {
  id: number;
  name: string;
  sku?: string;
  description?: string;
  base_price: number;
  unit_cost?: number;
  is_active: boolean;
  product_type_id?: number;
  collection_id?: number;
  product_type?: ProductType;
  collection?: Collection;
  colors?: Color[];
}

export interface Warehouse {
  id: number;
  name: string;
  code?: string;
  address?: string;
  city?: string;
  is_active?: boolean;
  is_pos?: boolean;
  warehouse_type?: WarehouseType;
  // Virtual Shopify warehouse fields (populated client-side)
  source?: 'mysql' | 'shopify';
  shopify_location_id?: number;
}

export interface ShopifyLocation {
  id: number;
  name: string;
  active: boolean;
  address?: string | null;
  city?: string | null;
}

export interface Stock {
  id: number;
  product_id: number;
  warehouse_id: number;
  color_id?: number;
  size?: string;
  quantity: number;
  reserved?: number;
  min_quantity?: number;
  reason?: string;
  available?: number;
  product?: Product;
  warehouse?: Warehouse;
  color?: Color;
}

export interface ProductLookupItem {
  stock_id: number | null;
  product_id: number;
  product_name: string;
  sku?: string;
  warehouse_id?: number | null;
  warehouse_name?: string | null;
  color_id?: number | null;
  color_name?: string | null;
  size?: string | null;
  available_qty?: number;
  unit_price?: number;
  unit_cost?: number;
  variant_label?: string;
  // Shopify-source fields (null for MySQL items)
  source?: 'mysql' | 'shopify';
  shopify_variant_id?: number | null;
  shopify_product_id?: number | null;
  shopify_location_id?: number | null;
  shopify_inventory_item_id?: number | null;
  image_url?: string | null;
}

// ── Orders ───────────────────────────────────────────────────────────────────

export interface OrderItem {
  id?: number;
  product_id?: number | null;
  color_id?: number | null;
  collection_id?: number | null;
  product_description?: string;
  product_key?: string | null;
  tracking_number?: string | null;
  size?: string | null;
  quantity: number;
  unit_price: number;
  subtotal: number;
  sort_order?: number;
  product?: Product;
  color?: Color;
}

export interface Order {
  id: number;
  order_number?: string;
  order_date: string;
  order_status_id?: number;
  order_status?: OrderStatus;
  shipping_agency_id?: number;
  shipping_agency?: ShippingAgency;
  purchase_type_id?: number;
  purchase_type?: PurchaseType;
  warehouse_id?: number;
  warehouse?: Warehouse;
  customer_id?: number;
  customer?: Customer;
  customer_name?: string;
  dni?: string;
  phone?: string;
  customer_email?: string;
  address?: string;
  pickup_key?: string;
  tracking_number?: string;
  province_id?: number;
  province?: Province;
  district_id?: number;
  district?: District;
  delivery_cost?: number;
  total: number;
  observations?: string;
  document_type_id?: number;
  document_print_format_id?: number;
  document_type?: DocumentType;
  document_print_format?: DocumentPrintFormat;
  documentPrintFormat?: DocumentPrintFormat;
  document_number?: string;
  guide_transfer_reason_code?: string;
  guide_transfer_reason_description?: string;
  guide_transfer_mode?: string;
  guide_transfer_date?: string;
  guide_total_weight?: number;
  guide_weight_unit?: string;
  guide_package_count?: number;
  guide_origin_ubigeo?: string;
  guide_origin_address?: string;
  guide_destination_ubigeo?: string;
  guide_destination_address?: string;
  guide_recipient_doc_type?: string;
  guide_recipient_doc_number?: string;
  guide_recipient_name?: string;
  guide_carrier_doc_type?: string;
  guide_carrier_doc_number?: string;
  guide_carrier_name?: string;
  guide_vehicle_plate?: string;
  guide_driver_doc_type?: string;
  guide_driver_doc_number?: string;
  guide_driver_name?: string;
  guide_driver_license?: string;
  guide_transport_certificate?: string;
  guide_series?: string;
  guide_correlativo?: number;
  guide_full_number?: string;
  guide_status?: string;
  guide_sunat_code?: number;
  guide_sunat_description?: string;
  guide_sent_at?: string;
  needs_receipt?: boolean;
  user_id?: number;
  user?: Pick<AppUser, 'id' | 'name'>;
  items?: OrderItem[];
  invoices?: { id: number; status: string; doc_type: string; full_number: string }[];
  created_at?: string;
}

export interface OrderUpsertRequest {
  order_date: string;
  order_status_id: number | null;
  shipping_agency_id?: number | null;
  purchase_type_id?: number | null;
  warehouse_id?: number | null;
  observations?: string | null;
  phone?: string | null;
  customer_id?: number | null;
  customer_name?: string | null;
  dni?: string | null;
  province_id?: number | null;
  district_id?: number | null;
  address?: string | null;
  pickup_key?: string | null;
  tracking_number?: string | null;
  delivery_cost?: number;
  discount_type?: 'percent' | 'fixed' | null;
  discount_value?: number | null;
  discount_amount?: number;
  total: number;
  document_type_id?: number | null;
  customer_email?: string | null;
  guide_transfer_reason_code?: string | null;
  guide_transfer_reason_description?: string | null;
  guide_transfer_mode?: string | null;
  guide_transfer_date?: string | null;
  guide_total_weight?: number | null;
  guide_weight_unit?: string | null;
  guide_package_count?: number | null;
  guide_origin_ubigeo?: string | null;
  guide_origin_address?: string | null;
  guide_destination_ubigeo?: string | null;
  guide_destination_address?: string | null;
  guide_recipient_doc_type?: string | null;
  guide_recipient_doc_number?: string | null;
  guide_recipient_name?: string | null;
  guide_carrier_doc_type?: string | null;
  guide_carrier_doc_number?: string | null;
  guide_carrier_name?: string | null;
  guide_vehicle_plate?: string | null;
  guide_driver_doc_type?: string | null;
  guide_driver_doc_number?: string | null;
  guide_driver_name?: string | null;
  guide_driver_license?: string | null;
  guide_transport_certificate?: string | null;
  items: OrderItem[];
}

export interface PosOrderCreateRequest {
  order_date: string;
  warehouse_id: number;
  payment_method_id: number;
  document_type_id: number;
  document_print_format_id?: number | null;
  order_status_id?: number | null;
  observations?: string | null;
  discount_type?: 'percent' | 'fixed' | null;
  discount_value?: number | null;
  discount_amount?: number;
  total: number;
  customer_id?: number | null;
  customer_name?: string | null;
  customer_document?: string | null;
  customer_document_type?: string | null;
  customer_email?: string | null;
  phone?: string | null;
  address?: string | null;
  print_after_save?: boolean;
  items: OrderItem[];
}

export interface PosOrderCreateResponse {
  id: number;
  order_number?: string;
  document_number?: string;
  pdf_url?: string | null;
  print_payload?: unknown;
}

// ── Promotions ───────────────────────────────────────────────────────────────

export interface PromotionItem {
  id?: number;
  promotion_id?: number;
  product_type_id?: number | null;
  product_id?: number | null;       // legacy, kept for backward compat
  quantity: number;
  unit_price?: number;
  notes?: string;
  product_type?: ProductType;
  product?: Product;                // legacy
}

export interface Promotion {
  id: number;
  name: string;
  description?: string;
  is_active: boolean;
  fixed_price?: number;
  total_price?: number;
  items?: PromotionItem[];
}

// ── Sales (imported) ─────────────────────────────────────────────────────────

export interface SaleItem {
  id?: number;
  sale_id?: number;
  product_name?: string;
  sku?: string;
  variant?: string;
  quantity: number;
  unit_gross_price: number;
  total_gross: number;
}

export interface Sale {
  id?: number;
  series_number?: string;
  document_type_label?: string;
  sale_datetime?: string;
  branch?: string;
  seller?: string;
  customer_name?: string;
  customer_tax_id?: string;
  currency?: string;
  total_gross: number;
  total_net?: number;
  total_tax?: number;
  user?: Pick<AppUser, 'id' | 'name'>;
  items?: SaleItem[];
}

// ── Invoicing / Facturación electrónica ──────────────────────────────────────

export interface InvoiceSeries {
  id: number;
  doc_type: string;     // '01'=Factura, '03'=Boleta, '07'=NC-Factura, '08'=NC-Boleta
  serie: string;        // F001, B001, FC01, BC01
  next_number: number;
  is_active: boolean;
}

export type InvoiceStatus =
  | 'draft'
  | 'generated'
  | 'sending'
  | 'pending'
  | 'sent'
  | 'accepted'
  | 'accepted_with_obs'
  | 'rejected'
  | 'exception'
  | 'error_connection'
  | 'error_validation'
  | 'error_envio'
  | 'error_sunat'
  | 'error'
  | 'cancelled'
  | 'pending_daily_summary'
  | 'daily_summary_sent'
  | 'ticket_generated'
  | 'processing';

export interface Invoice {
  id: number;
  order_id?: number;
  order?: Order;
  invoice_series_id: number;
  doc_type: string;         // '01', '03', '07', '08'
  serie: string;
  correlativo: number;
  full_number: string;
  status: InvoiceStatus;
  doc_type_label?: string;
  status_label?: string;
  customer_doc_type?: string;   // '1'=DNI, '6'=RUC, '4'=CE, '7'=PAS, '-'=sin doc
  customer_doc_number?: string;
  customer_name?: string;
  currency: string;
  form_of_payment: string;
  mto_oper_gravadas: number;
  mto_igv: number;
  valor_venta: number;
  sub_total: number;
  mto_imp_venta: number;
  sunat_code?: number;
  sunat_description?: string;
  sunat_notes?: string[];
  xml_content?: string;
  cdr_content?: string;
  note_motive?: string;
  note_motive_desc?: string;
  ref_doc_type?: string;
  ref_doc_number?: string;
  ref_doc_date?: string;
  observations?: string;
  issued_at: string;
  user?: Pick<AppUser, 'id' | 'name'>;
  created_at?: string;
}

export interface DailySummaryItem {
  id: number;
  invoice_id: number;
  doc_type: string;
  serie: string;
  correlativo: number;
  customer_doc_type?: string;
  customer_doc_number?: string;
  total: number;
  status: string;
  invoice?: Invoice;
}

export interface DailySummary {
  id: number;
  summary_date: string;
  summary_number: string;
  file_name?: string;
  status: string;
  ticket?: string;
  sunat_code?: number;
  sunat_description?: string;
  sunat_notes?: string[];
  sent_at?: string;
  accepted_at?: string;
  rejected_at?: string;
  items_count?: number;
  items?: DailySummaryItem[];
}

export interface PosInitialData {
  warehouses: Warehouse[];
  document_types: DocumentType[];
  payment_methods: PaymentMethod[];
  colors: Color[];
  settings: Record<string, Setting>;
}

// ── Pagination ───────────────────────────────────────────────────────────────

export interface Page<T> {
  data: T[];
  current_page: number;
  last_page: number;
  per_page: number;
  total: number;
}

// ── Shopify ───────────────────────────────────────────────────────────────────

export interface ShopifyOrderItem {
  id: number;
  title: string;
  variant_title?: string | null;
  quantity: number;
  price: number;
  sku?: string | null;
  fulfillment_status?: string | null;
}

export interface ShopifyDiscountCode {
  code: string;
  amount: number;
  type: string;  // percentage | fixed_amount | shipping
}

export interface ShopifyShippingLine {
  title: string;
  price: number;
  discounted_price: number;
  is_free: boolean;
}

export interface ShopifyOrder {
  id: number;
  order_number: string;
  created_at: string;
  updated_at: string;
  financial_status?: string | null;
  fulfillment_status?: string | null;
  total_price: number;
  subtotal_price?: number;
  total_discounts?: number;
  has_free_shipping?: boolean;
  is_local_pickup?: boolean;
  currency: string;
  customer_name?: string | null;
  customer_email?: string | null;
  customer_phone?: string | null;
  customer_document?: string | null;
  shipping_address?: string | null;
  province?: string | null;
  city?: string | null;
  tracking_number?: string | null;
  tracking_company?: string | null;
  tracking_url?: string | null;
  note?: string | null;
  tags?: string | null;
  cancel_reason?: string | null;
  is_cancelled: boolean;
  discount_codes?: ShopifyDiscountCode[];
  shipping_lines?: ShopifyShippingLine[];
  items: ShopifyOrderItem[];
}

export interface ShopifyOrderListResponse {
  orders: ShopifyOrder[];
  count: number;
  next_page_info?: string | null;
  prev_page_info?: string | null;
}
