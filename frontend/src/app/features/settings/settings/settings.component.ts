import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

interface ConfigItem {
  title: string;
  description: string;
  path: string;
  icon: string;
}

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
})
export class SettingsComponent {
  configItems: ConfigItem[] = [
    { title: 'Roles',               description: 'Gestión de roles y permisos',              path: '/dashboard/settings/roles',            icon: 'bi-shield-check'  },
    { title: 'Estados de pedido',   description: 'En camino, Entregado, Pendiente...',       path: '/dashboard/settings/order-statuses',  icon: 'bi-tag'           },
    { title: 'Agencias de envío',   description: 'SHALOM, OLVA, Serpost...',                 path: '/dashboard/settings/shipping-agencies', icon: 'bi-truck'         },
    { title: 'Tipos de producto',   description: 'Polo, Camisa, Pantalón...',                path: '/dashboard/settings/product-types',    icon: 'bi-grid'          },
    { title: 'Almacenes',           description: 'Ubicaciones de stock y puntos de venta',   path: '/dashboard/settings/warehouses',       icon: 'bi-building'      },
    { title: 'Colecciones',         description: 'Líneas o colecciones',                     path: '/dashboard/settings/collections',      icon: 'bi-collection'    },
    { title: 'Unidades de medida',  description: 'NIU, KGM, LTR y códigos SUNAT',            path: '/dashboard/settings/unit-measures',    icon: 'bi-rulers'        },
    { title: 'Provincias y distritos', description: 'Ubicación geográfica',                  path: '/dashboard/settings/provinces',        icon: 'bi-geo-alt'       },
    { title: 'Parámetros fiscales', description: 'IGV, RUC, moneda, empresa y series de comprobantes', path: '/dashboard/settings/fiscal', icon: 'bi-percent' },
    { title: 'Series y correlativos', description: 'FFF1, BBB1, TTT1 — siguiente número de cada serie', path: '/dashboard/settings/fiscal', icon: 'bi-receipt'       },
    { title: 'Métodos de pago',     description: 'Efectivo, Yape, Plin, Transferencia...',   path: '/dashboard/settings/payment-methods',  icon: 'bi-credit-card'   },
  ];
}
