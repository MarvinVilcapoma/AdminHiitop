# Hiitop – Tienda de ropa

Aplicación de administración para tienda de ropa: **frontend Angular** (Angular Material) y **API Laravel** (CRUDs, auth, roles).

## Estructura

- **`api/`** – Backend Laravel 11 (Sanctum, Spatie Permission, migraciones, modelos, controladores API).
- **`frontend/`** – SPA Angular 19 con Angular Material: login (diseño dos paneles con logo Hiitop), dashboard con menú lateral negro tipo hamburguesa y pantallas en blanco, gestión de pedidos, inventario, clientes y configuración (CRUDs de catálogos).

## Requisitos

- **API**: PHP 8.2+, Composer, MySQL.
- **Frontend**: Node.js 18+, npm.

**¿Cómo conectar la base de datos y probar la API en local?** → Ver **[SETUP.md](SETUP.md)** (crear BD, configurar `.env`, migrar, seed, usuario de prueba y probar el login).

**¿Usar Docker (api-hiitop, front-hiitop, MySQL hiitop-db)?** → Ver **[DOCKER.md](DOCKER.md)**. Docker es gratuito para uso personal y pequeñas empresas.

## Instalación rápida

### 1. API Laravel

Si la carpeta `api` no tiene el framework instalado (no existe `api/vendor`):

1. Crear un proyecto Laravel nuevo y copiar nuestro código sobre él:
   ```bash
   composer create-project laravel/laravel api-temp
   # Copiar el contenido de api/ (este repo) sobre api-temp, o al revés según prefieras
   ```
2. O desde dentro de `api` (si ya tienes nuestro código):
   ```bash
   cd api
   composer install
   composer require laravel/sanctum spatie/laravel-permission
   php artisan vendor:publish --provider="Laravel\Sanctum\SanctumServiceProvider"
   php artisan vendor:publish --provider="Spatie\Permission\PermissionServiceProvider"
   cp .env.example .env
   php artisan key:generate
   ```
3. Configurar `.env` con tu base de datos y ejecutar:
   ```bash
   php artisan migrate
   php artisan db:seed
   php artisan serve
   ```
   API en **http://localhost:8000**. Usuario por defecto: `admin@hiitop.com` / `password`.

### 2. Frontend Angular

```bash
cd frontend
npm install
npm start
```

Abre **http://localhost:4200**. Ajusta `src/environments/environment.ts` si la API no está en `http://localhost:8000/api`.

## Funcionalidad principal

- **Login**: Panel izquierdo con logo Hiitop (verde) y eslogan; panel derecho con formulario (email/password). Registro “Sign up for free”.
- **Dashboard**: Menú lateral negro (hamburguesa), barra superior con búsqueda, contenido en blanco.
- **Pedidos**: Listado con fecha, estado, agencia, DNI, cliente, provincia, distrito, total; tarjetas de resumen; paginación; formulario nuevo/editar.
- **Inventario**: Listado de stock por producto, almacén, color, talla.
- **Clientes**: Listado con nombre, DNI, teléfono, email, distrito.
- **Configuración**: Acceso a CRUDs de: estados de pedido, agencias de envío, tipos de comprobante, tipos de compra, tipos de producto, colores, almacenes, colecciones, productos, provincias.

La API expone endpoints REST para todos estos recursos y usa **Laravel Sanctum** para autenticación y **Spatie Laravel Permission** para roles/permisos (semilla con admin y permisos básicos).
