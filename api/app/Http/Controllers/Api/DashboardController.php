<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Customer;
use App\Models\Order;
use App\Models\OrderStatus;
use App\Models\SaleImport;
use App\Models\Stock;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Carbon;
use Illuminate\Support\Collection;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

class DashboardController extends Controller
{
    private const ORDERS_ALIAS = 'orders as o';
    private const ORDER_ITEMS_ALIAS = 'order_items as oi';

    public function index(Request $request): JsonResponse
    {
        $from = $request->filled('from')
            ? Carbon::parse($request->from)->startOfDay()
            : Carbon::now()->startOfMonth()->startOfDay();

        $to = $request->filled('to')
            ? Carbon::parse($request->to)->endOfDay()
            : Carbon::now()->endOfDay();

        $cancelledStatusIds = OrderStatus::query()
            ->whereIn('slug', ['cancelado', 'cancelled'])
            ->pluck('id');

        // -- Metricas principales --
        $ordersInRange = Order::whereBetween('order_date', [$from, $to]);
        if ($cancelledStatusIds->isNotEmpty()) {
            $ordersInRange->whereNotIn('order_status_id', $cancelledStatusIds);
        }

        $totalOrders   = (clone $ordersInRange)->count();
        $totalRevenue  = (clone $ordersInRange)->sum('total');
        $avgTicket     = $totalOrders > 0 ? $totalRevenue / $totalOrders : 0;
        $pendingOrders = Order::whereHas('orderStatus', fn ($q) => $q->whereIn('slug', ['pendiente', 'pending', 'en-proceso', 'en-camino']))->count();
        $newCustomers  = Customer::whereBetween('created_at', [$from, $to])->count();

        $posStatusId = OrderStatus::query()->where('slug', 'venta-pos')->value('id');
        $posOrdersInRange = Order::query()
            ->whereBetween('order_date', [$from, $to])
            ->where(function ($q) use ($posStatusId) {
                if ($posStatusId) {
                    $q->where('order_status_id', $posStatusId)
                        ->orWhere('observations', 'like', 'POS%');
                    return;
                }

                $q->where('observations', 'like', 'POS%');
            });

        if ($cancelledStatusIds->isNotEmpty()) {
            $posOrdersInRange->whereNotIn('order_status_id', $cancelledStatusIds);
        }

        $posSalesCount = (clone $posOrdersInRange)->count();
        $posSalesRevenue = (float) (clone $posOrdersInRange)->sum('total');

        // -- Total unidades vendidas --
        $totalUnitsQuery = DB::table(self::ORDER_ITEMS_ALIAS)
            ->join(self::ORDERS_ALIAS, 'o.id', '=', 'oi.order_id')
            ->whereBetween('o.order_date', [$from, $to])
            ->whereNull('o.deleted_at')
            ->whereNull('oi.deleted_at');

        if ($cancelledStatusIds->isNotEmpty()) {
            $totalUnitsQuery->whereNotIn('o.order_status_id', $cancelledStatusIds);
        }

        $totalUnits = $totalUnitsQuery->sum('oi.quantity');

        ['avgMarginPct' => $importedAvgMarginPct, 'byBranch' => $byBranch] = $this->salesAnalytics($from, $to);
        ['totalCost' => $totalCost, 'totalProfit' => $totalProfit, 'marginPct' => $ordersMarginPct] = $this->orderMarginAnalytics($from, $to, $cancelledStatusIds);

        $avgMarginPct = $ordersMarginPct ?? $importedAvgMarginPct;
        $byPaymentMethod = $this->paymentMethodAnalytics($from, $to);

        // -- Ventas por vendedor (desde pedidos) --
        $bySeller = DB::table(self::ORDERS_ALIAS)
            ->join('users as u', 'u.id', '=', 'o.user_id')
            ->whereBetween('o.order_date', [$from, $to])
            ->whereNull('o.deleted_at')
            ->whereNull('u.deleted_at')
            ->select(
                'u.name as seller',
                DB::raw('COUNT(o.id) as total_orders'),
                DB::raw('SUM(o.total) as total_revenue')
            )
            ->groupBy('u.name')
            ->orderByDesc('total_revenue')
            ->limit(10)
            ->get()
            ->map(fn ($r) => [
                'seller'        => $r->seller,
                'total_orders'  => (int) $r->total_orders,
                'total_revenue' => round((float) $r->total_revenue, 2),
                'avg_ticket'    => $r->total_orders > 0
                    ? round($r->total_revenue / $r->total_orders, 2)
                    : 0,
            ]);

        if ($cancelledStatusIds->isNotEmpty()) {
            $bySeller = DB::table(self::ORDERS_ALIAS)
                ->join('users as u', 'u.id', '=', 'o.user_id')
                ->whereBetween('o.order_date', [$from, $to])
                ->whereNull('o.deleted_at')
                ->whereNull('u.deleted_at')
                ->whereNotIn('o.order_status_id', $cancelledStatusIds)
                ->select(
                    'u.name as seller',
                    DB::raw('COUNT(o.id) as total_orders'),
                    DB::raw('SUM(o.total) as total_revenue')
                )
                ->groupBy('u.name')
                ->orderByDesc('total_revenue')
                ->limit(10)
                ->get()
                ->map(fn ($r) => [
                    'seller'        => $r->seller,
                    'total_orders'  => (int) $r->total_orders,
                    'total_revenue' => round((float) $r->total_revenue, 2),
                    'avg_ticket'    => $r->total_orders > 0
                        ? round($r->total_revenue / $r->total_orders, 2)
                        : 0,
                ]);
        }

        // -- Ventas por dia --
        $salesByDayQuery = Order::whereBetween('order_date', [$from, $to])
            ->select(DB::raw('DATE(order_date) as date'), DB::raw('COUNT(*) as orders'), DB::raw('SUM(total) as revenue'))
            ->groupBy('date')
            ->orderBy('date');

        if ($cancelledStatusIds->isNotEmpty()) {
            $salesByDayQuery->whereNotIn('order_status_id', $cancelledStatusIds);
        }

        $salesByDay = $salesByDayQuery->get();

        // -- Top 10 productos --
        $topProductsQuery = DB::table(self::ORDER_ITEMS_ALIAS)
            ->join(self::ORDERS_ALIAS, 'o.id', '=', 'oi.order_id')
            ->whereBetween('o.order_date', [$from, $to])
            ->select('oi.product_description', DB::raw('SUM(oi.quantity) as total_qty'), DB::raw('SUM(oi.subtotal) as total_revenue'))
            ->groupBy('oi.product_description')
            ->orderByDesc('total_revenue')
            ->limit(10);

        if ($cancelledStatusIds->isNotEmpty()) {
            $topProductsQuery->whereNotIn('o.order_status_id', $cancelledStatusIds);
        }

        $topProducts = $topProductsQuery->get();

        // -- Ventas por estado --
        $byStatus = Order::whereBetween('order_date', [$from, $to])
            ->with('orderStatus:id,name,color')
            ->select('order_status_id', DB::raw('COUNT(*) as count'), DB::raw('SUM(total) as revenue'))
            ->groupBy('order_status_id')
            ->get()
            ->map(fn ($r) => [
                'status'  => $r->orderStatus?->name ?? 'Sin estado',
                'color'   => $r->orderStatus?->color ?? '#ccc',
                'count'   => $r->count,
                'revenue' => (float) $r->revenue,
            ]);

        // -- Ventas por agencia de envio --
        $byAgencyQuery = Order::whereBetween('order_date', [$from, $to])
            ->with('shippingAgency:id,name')
            ->whereNotNull('shipping_agency_id')
            ->select('shipping_agency_id', DB::raw('COUNT(*) as count'))
            ->groupBy('shipping_agency_id');

        if ($cancelledStatusIds->isNotEmpty()) {
            $byAgencyQuery->whereNotIn('order_status_id', $cancelledStatusIds);
        }

        $byAgency = $byAgencyQuery->get()
            ->map(fn ($r) => [
                'agency' => $r->shippingAgency?->name ?? '-',
                'count'  => $r->count,
            ]);

        // -- Pedidos recientes --
        $recentOrders = Order::with(['orderStatus:id,name,color', 'shippingAgency:id,name'])
            ->orderByDesc('order_date')
            ->orderByDesc('id')
            ->limit(8)
            ->get(['id', 'order_date', 'customer_name', 'total', 'order_status_id', 'shipping_agency_id', 'phone']);

        // -- Stock bajo (qty <= 5) --
        $lowStock = Stock::with(['product:id,name,sku', 'warehouse:id,name,type', 'color:id,name'])
            ->where('quantity', '<=', 5)
            ->orderBy('quantity')
            ->limit(10)
            ->get();

        $importedTopSkus = $this->importedTopSkus($from, $to);

        return response()->json([
            'period'            => ['from' => $from->toDateString(), 'to' => $to->toDateString()],
            'summary'           => [
                'total_orders'   => $totalOrders,
                'total_revenue'  => round((float) $totalRevenue, 2),
                'avg_ticket'     => round($avgTicket, 2),
                'total_units'    => (int) $totalUnits,
                'total_cost'     => $totalCost,
                'total_profit'   => $totalProfit,
                'avg_margin_pct' => $avgMarginPct,
                'pos_sales_count' => (int) $posSalesCount,
                'pos_sales_revenue' => round($posSalesRevenue, 2),
                'pending_orders' => $pendingOrders,
                'new_customers'  => $newCustomers,
            ],
            'sales_by_day'      => $salesByDay,
            'top_products'      => $topProducts,
            'by_status'         => $byStatus,
            'by_agency'         => $byAgency,
            'recent_orders'     => $recentOrders,
            'low_stock'         => $lowStock,
            'imported_top_skus' => $importedTopSkus,
            'by_branch'         => $byBranch,
            'by_payment_method' => $byPaymentMethod,
            'by_seller'         => $bySeller,
        ]);
    }

    private function salesAnalytics(Carbon $from, Carbon $to): array
    {
        $hasSalesAnalytics =
            Schema::hasTable('sales')
            && Schema::hasTable('sale_items')
            && Schema::hasColumn('sales', 'id')
            && Schema::hasColumn('sales', 'issue_date')
            && Schema::hasColumn('sales', 'branch')
            && Schema::hasColumn('sales', 'total_gross')
            && Schema::hasColumn('sale_items', 'sale_id')
            && Schema::hasColumn('sale_items', 'margin_pct');

        if (!$hasSalesAnalytics) {
            return ['avgMarginPct' => null, 'byBranch' => collect()];
        }

        $marginRow = DB::table('sale_items as si')
            ->join('sales as s', 's.id', '=', 'si.sale_id')
            ->whereBetween('s.issue_date', [$from->toDateString(), $to->toDateString()])
            ->whereNotNull('si.margin_pct')
            ->selectRaw('AVG(si.margin_pct) as avg_margin_pct')
            ->first();

        $avgMarginPct = $marginRow ? round((float) $marginRow->avg_margin_pct, 2) : null;

        $byBranch = DB::table('sales')
            ->whereBetween('issue_date', [$from->toDateString(), $to->toDateString()])
            ->whereNotNull('branch')
            ->where('branch', '!=', '')
            ->select('branch', DB::raw('COUNT(*) as total_orders'), DB::raw('SUM(total_gross) as total_revenue'))
            ->groupBy('branch')
            ->orderByDesc('total_revenue')
            ->get();

        return ['avgMarginPct' => $avgMarginPct, 'byBranch' => $byBranch];
    }

    private function paymentMethodAnalytics(Carbon $from, Carbon $to): Collection
    {
        $hasPaymentAnalytics =
            Schema::hasTable('invoices')
            && Schema::hasTable('payment_methods')
            && Schema::hasTable('orders')
            && Schema::hasColumn('invoices', 'payment_method_id')
            && Schema::hasColumn('invoices', 'order_id')
            && Schema::hasColumn('invoices', 'mto_imp_venta')
            && Schema::hasColumn('invoices', 'status')
            && Schema::hasColumn('orders', 'order_date');

        if (!$hasPaymentAnalytics) {
            return collect();
        }

        return DB::table('invoices as i')
            ->join('payment_methods as pm', 'pm.id', '=', 'i.payment_method_id')
            ->join(self::ORDERS_ALIAS, 'o.id', '=', 'i.order_id')
            ->whereBetween('o.order_date', [$from, $to])
            ->whereNull('i.deleted_at')
            ->whereNull('o.deleted_at')
            ->where('i.status', '!=', 'cancelled')
            ->select('pm.name as method', DB::raw('SUM(i.mto_imp_venta) as total'))
            ->groupBy('pm.name')
            ->orderByDesc('total')
            ->get();
    }

    private function orderMarginAnalytics(Carbon $from, Carbon $to, Collection $excludedStatusIds): array
    {
        $hasCostAnalytics =
            Schema::hasTable('order_items')
            && Schema::hasTable('products')
            && Schema::hasColumn('order_items', 'order_id')
            && Schema::hasColumn('order_items', 'product_id')
            && Schema::hasColumn('order_items', 'quantity')
            && Schema::hasColumn('order_items', 'subtotal')
            && Schema::hasColumn('products', 'id')
            && Schema::hasColumn('products', 'unit_cost');

        if (!$hasCostAnalytics) {
            return [
                'totalCost' => 0.0,
                'totalProfit' => 0.0,
                'marginPct' => null,
            ];
        }

        $query = DB::table(self::ORDER_ITEMS_ALIAS)
            ->join(self::ORDERS_ALIAS, 'o.id', '=', 'oi.order_id')
            ->leftJoin('products as p', 'p.id', '=', 'oi.product_id')
            ->whereBetween('o.order_date', [$from, $to])
            ->whereNull('o.deleted_at');

        if ($excludedStatusIds->isNotEmpty()) {
            $query->whereNotIn('o.order_status_id', $excludedStatusIds);
        }

        if (Schema::hasColumn('order_items', 'deleted_at')) {
            $query->whereNull('oi.deleted_at');
        }

        $row = $query
            ->selectRaw('SUM(oi.subtotal) as sales_revenue, SUM(oi.quantity * COALESCE(p.unit_cost, 0)) as total_cost')
            ->first();

        $salesRevenue = (float) ($row->sales_revenue ?? 0);
        $totalCost = round((float) ($row->total_cost ?? 0), 2);
        $totalProfit = round($salesRevenue - $totalCost, 2);
        $marginPct = $salesRevenue > 0
            ? round(($totalProfit / $salesRevenue) * 100, 2)
            : null;

        return [
            'totalCost' => $totalCost,
            'totalProfit' => $totalProfit,
            'marginPct' => $marginPct,
        ];
    }

    private function importedTopSkus(Carbon $from, Carbon $to): Collection
    {
        $hasSaleImports =
            Schema::hasTable('sale_imports')
            && Schema::hasColumn('sale_imports', 'issue_date')
            && Schema::hasColumn('sale_imports', 'sku')
            && Schema::hasColumn('sale_imports', 'product_name')
            && Schema::hasColumn('sale_imports', 'quantity')
            && Schema::hasColumn('sale_imports', 'total_net');

        if (!$hasSaleImports) {
            return collect();
        }

        return SaleImport::whereBetween('issue_date', [$from->toDateString(), $to->toDateString()])
            ->select('sku', 'product_name', DB::raw('SUM(quantity) as total_qty'), DB::raw('SUM(total_net) as total_net'))
            ->whereNotNull('sku')
            ->groupBy('sku', 'product_name')
            ->orderByDesc('total_qty')
            ->limit(10)
            ->get();
    }
}
