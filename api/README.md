# Hiitop API (Laravel)

Backend para la tienda de ropa Hiitop. Laravel 11 + Sanctum + Spatie Permission.

## Requisitos

- PHP 8.2+
- Composer
- MySQL o MariaDB

## Instalación

1. **Crear proyecto Laravel** (si esta carpeta está vacía de vendor):
   ```bash
   composer create-project laravel/laravel temp-api
   mv temp-api/* temp-api/.* . 2>/dev/null; rmdir temp-api
   ```
   O desde la raíz del repo, si `api` ya tiene este código:
   ```bash
   cd api
   composer install
   ```

2. **Instalar dependencias adicionales**
   ```bash
   composer require laravel/sanctum spatie/laravel-permission
   php artisan vendor:publish --provider="Laravel\Sanctum\SanctumServiceProvider"
   php artisan vendor:publish --provider="Spatie\Permission\PermissionServiceProvider"
   ```

3. **Configuración**
   ```bash
   cp .env.example .env
   php artisan key:generate
   ```
   Editar `.env` con tu base de datos.

4. **Migraciones y seeders**
   ```bash
   php artisan migrate
   php artisan db:seed
   ```

5. **Ejecutar**
   ```bash
   php artisan serve
   ```
   API en `http://localhost:8000`. Endpoints bajo `/api`.

## Usuario por defecto

- Email: `admin@hiitop.com`
- Password: `password`

## Endpoints principales

- `POST /api/login` — Login (email, password)
- `GET /api/me` — Usuario actual (requiere token)
- `GET /api/orders` — Listado pedidos (con ?with_summary=1 para dashboard)
- CRUD: `/api/order-statuses`, `/api/shipping-agencies`, `/api/document-types`, `/api/products`, `/api/customers`, etc.
