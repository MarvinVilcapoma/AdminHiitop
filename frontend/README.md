# Hiitop Frontend (Angular)

Panel de administración para la tienda de ropa Hiitop. Angular 19 + Angular Material.

## Requisitos

- Node.js 18+
- npm o yarn

## Instalación

```bash
cd frontend
npm install
```

## Configuración

Editar `src/environments/environment.ts` y ajustar `apiUrl` si la API no está en `http://localhost:8000/api`.

## Desarrollo

```bash
npm start
```

Abre http://localhost:4200. El login por defecto (cuando la API esté corriendo y con seeders) es:

- Email: `admin@hiitop.com`
- Contraseña: `password`

## Build producción

```bash
npm run build
```

Salida en `dist/hiitop-frontend`.

## Estructura

- **core**: Auth (servicio, guard, interceptor), ApiService
- **layout**: Dashboard con menú lateral negro (hamburguesa) y contenido blanco
- **features/auth**: Login (diseño dos paneles con logo Hiitop)
- **features/orders**: Listado y formulario de pedidos
- **features/inventory**: Listado de stock
- **features/customers**: Listado de clientes
- **features/settings**: Configuración y CRUDs de catálogos (estados, agencias, productos, colores, almacenes, etc.)
