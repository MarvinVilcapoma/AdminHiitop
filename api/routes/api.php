<?php

use App\Http\Controllers\Api\AuthController;
use App\Http\Controllers\Api\ColorController;
use App\Http\Controllers\Api\CollectionController;
use App\Http\Controllers\Api\CustomerController;
use App\Http\Controllers\Api\DashboardController;
use App\Http\Controllers\Api\DistrictController;
use App\Http\Controllers\Api\DocumentTypeController;
use App\Http\Controllers\Api\InvoiceController;
use App\Http\Controllers\Api\OrderController;
use App\Http\Controllers\Api\OrderGuideController;
use App\Http\Controllers\Api\OrderStatusController;
use App\Http\Controllers\Api\PaymentMethodController;
use App\Http\Controllers\Api\ProductController;
use App\Http\Controllers\Api\ProductTypeController;
use App\Http\Controllers\Api\ProvinceController;
use App\Http\Controllers\Api\PurchaseTypeController;
use App\Http\Controllers\Api\SaleImportController;
use App\Http\Controllers\Api\RoleController;
use App\Http\Controllers\Api\ShippingAgencyController;
use App\Http\Controllers\Api\SettingController;
use App\Http\Controllers\SaleController;
use App\Http\Controllers\PromotionController;
use App\Http\Controllers\Api\StockController;
use App\Http\Controllers\Api\UserController;
use App\Http\Controllers\Api\UnitMeasureController;
use App\Http\Controllers\Api\WarehouseController;
use App\Http\Controllers\Api\WarehouseTypeController;
use Illuminate\Support\Facades\Route;

// Público
Route::post('login', [AuthController::class, 'login']);
Route::post('register', [AuthController::class, 'register']);

// Protegido con Sanctum
Route::middleware('auth:sanctum')->group(function () {
    Route::post('logout', [AuthController::class, 'logout']);
    Route::get('me', [AuthController::class, 'me']);

    // Dashboard
    Route::get('dashboard', [DashboardController::class, 'index']);

    // Facturacion electronica
    Route::get('invoices/series',             [InvoiceController::class, 'series']);
    Route::post('invoices/test-connection',   [InvoiceController::class, 'testConnection']);
    Route::post('invoices/{invoice}/send',    [InvoiceController::class, 'send']);
    Route::get('invoices/{invoice}/xml',      [InvoiceController::class, 'downloadXml']);
    Route::get('invoices/{invoice}/cdr',      [InvoiceController::class, 'downloadCdr']);
    Route::get('invoices/{invoice}/pdf',      [InvoiceController::class, 'downloadPdf']);
    Route::post('invoices/{invoice}/void',    [InvoiceController::class, 'void']);
    Route::apiResource('invoices', InvoiceController::class);

    // Pedidos
    Route::get('orders', [OrderController::class, 'index']);
    Route::get('guides', [OrderGuideController::class, 'index']);
    Route::get('orders/{order}/guide', [OrderGuideController::class, 'show']);
    Route::post('orders/{order}/guide/send', [OrderGuideController::class, 'send']);
    Route::get('orders/{order}/guide/xml', [OrderGuideController::class, 'downloadXml']);
    Route::get('orders/{order}/guide/cdr', [OrderGuideController::class, 'downloadCdr']);
    Route::apiResource('orders', OrderController::class)->except('index');

    // Clientes
    Route::apiResource('customers', CustomerController::class);

    // Stock
    Route::get('stocks/summary',   [StockController::class, 'summary']);
    Route::get('stocks/available', [StockController::class, 'available']);
    Route::post('stocks/bulk',          [StockController::class, 'bulkStore']);
    Route::post('stocks/bulk-transfer', [StockController::class, 'bulkTransfer']);
    Route::post('stocks/{stock}/adjust',   [StockController::class, 'adjust']);
    Route::post('stocks/{stock}/transfer', [StockController::class, 'transfer']);
    Route::apiResource('stocks', StockController::class);

    // Importaciones de ventas
    Route::get('sale-imports',                      [SaleImportController::class, 'index']);
    Route::post('sale-imports/import',              [SaleImportController::class, 'import']);
    Route::get('sale-imports/summary',              [SaleImportController::class, 'summary']);
    Route::get('sale-imports/{batch}',              [SaleImportController::class, 'show']);
    Route::delete('sale-imports/{batch}/batch',     [SaleImportController::class, 'destroyBatch']);

    // Roles
    Route::get('roles/permissions',             [RoleController::class, 'permissions']);
    Route::apiResource('roles',                 RoleController::class);

    // Usuarios y roles
    Route::get('users/roles-list',                  [UserController::class, 'roles']);
    Route::apiResource('users', UserController::class);

    // Configuración - Catálogos
    Route::apiResource('order-statuses',    OrderStatusController::class);
    Route::apiResource('shipping-agencies', ShippingAgencyController::class);
    Route::apiResource('document-types',    DocumentTypeController::class);
    Route::apiResource('purchase-types',    PurchaseTypeController::class);
    Route::apiResource('product-types',     ProductTypeController::class);
    Route::post('product-types/{productType}/sizes', [ProductTypeController::class, 'syncSizes']);
    Route::apiResource('colors',            ColorController::class);
    Route::apiResource('warehouses',        WarehouseController::class);
    Route::apiResource('warehouse-types',    WarehouseTypeController::class);
    Route::apiResource('collections',       CollectionController::class);
    Route::apiResource('payment-methods',   PaymentMethodController::class);
    Route::apiResource('unit-measures',     UnitMeasureController::class);

    // Configuración del sistema
    Route::get('settings',        [SettingController::class, 'index']);
    Route::patch('settings',      [SettingController::class, 'update']);
    Route::put('settings/{key}',  [SettingController::class, 'updateKey']);

    // Productos e inventario
    Route::apiResource('products', ProductController::class);

    // Ventas manuales / Bsale
    Route::get('sales/branches', [SaleController::class, 'branches']);
    Route::apiResource('sales', SaleController::class);

    // Promociones
    Route::apiResource('promotions', PromotionController::class);

    // Ubicación
    Route::apiResource('provinces', ProvinceController::class);
    Route::apiResource('districts', DistrictController::class);
});
