# Guía de configuración – Hiitop

Pasos para tener la **API Laravel** y la **base de datos** funcionando en local y poder probar el login desde el frontend.

---

## 1. Base de datos (MySQL)

### Crear la base de datos

En MySQL (o MariaDB) crea la base y un usuario si lo necesitas:

```sql
CREATE DATABASE hiitop CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

Si usas **XAMPP**, **Laragon** o **WAMP**: abre phpMyAdmin o la consola de MySQL y ejecuta ese `CREATE DATABASE`.

Si usas **MySQL en Windows** (instalación propia):

```bash
mysql -u root -p
```

Luego en el prompt:

```sql
CREATE DATABASE hiitop CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
EXIT;
```

---

## 2. API Laravel

### Requisitos

- PHP 8.2 o superior  
- Composer  
- Extensión PHP para MySQL (`pdo_mysql`)

### Opción A: Proyecto Laravel nuevo y copiar código

1. En la raíz del proyecto (carpeta donde está `api` y `frontend`):

   ```bash
   composer create-project laravel/laravel api-laravel
   ```

2. Copia el contenido de la carpeta `api` (que ya tienes) **dentro** de `api-laravel`, sobrescribiendo archivos cuando pregunte (o copia a mano solo lo que falte: `app/Models`, `app/Http/Controllers/Api`, `routes/api.php`, `database/migrations`, `database/seeders`, `config/cors.php`, etc.).

3. Entra en la carpeta del proyecto Laravel:

   ```bash
   cd api-laravel
   ```

4. Instala dependencias extra:

   ```bash
   composer require laravel/sanctum spatie/laravel-permission
   ```

5. Publica configuración y migraciones de Sanctum y Permission:

   ```bash
   php artisan vendor:publish --provider="Laravel\Sanctum\SanctumServiceProvider"
   php artisan vendor:publish --provider="Spatie\Permission\PermissionServiceProvider"
   ```

6. Configura el `.env`:

   ```bash
   copy .env.example .env
   php artisan key:generate
   ```

   Edita `.env` y ajusta la base de datos:

   ```env
   DB_CONNECTION=mysql
   DB_HOST=127.0.0.1
   DB_PORT=3306
   DB_DATABASE=hiitop
   DB_USERNAME=root
   DB_PASSWORD=tu_contraseña_de_mysql
   ```

   (Si no tienes contraseña en `root`, deja `DB_PASSWORD=` vacío.)

7. Migraciones y datos iniciales:

   ```bash
   php artisan migrate
   php artisan db:seed
   ```

8. Arranca la API:

   ```bash
   php artisan serve
   ```

   La API quedará en **http://localhost:8000**.

### Opción B: Si ya tienes Laravel en la carpeta `api`

1. Entra en `api`:

   ```bash
   cd api
   ```

2. Si no hay carpeta `vendor`:

   ```bash
   composer install
   composer require laravel/sanctum spatie/laravel-permission
   php artisan vendor:publish --provider="Laravel\Sanctum\SanctumServiceProvider"
   php artisan vendor:publish --provider="Spatie\Permission\PermissionServiceProvider"
   ```

3. Crea y configura `.env`:

   ```bash
   copy .env.example .env
   php artisan key:generate
   ```

   En `.env` pon:

   - `DB_DATABASE=hiitop`
   - `DB_USERNAME` y `DB_PASSWORD` según tu MySQL.

4. Migrar y seed:

   ```bash
   php artisan migrate
   php artisan db:seed
   ```

5. Servir la API:

   ```bash
   php artisan serve
   ```

---

## 3. Probar la API en local

### Usuario por defecto (tras `db:seed`)

- **Email:** `admin@hiitop.com`  
- **Contraseña:** `password`  

### Probar login con curl (PowerShell)

```powershell
Invoke-RestMethod -Uri "http://localhost:8000/api/login" -Method POST -ContentType "application/json" -Body '{"email":"admin@hiitop.com","password":"password"}'
```

Si responde con `user`, `token`, etc., la API y la base de datos están bien conectadas.

### Probar en el navegador (frontend)

1. Arranca el frontend:

   ```bash
   cd frontend
   npm install
   npm start
   ```

2. Abre **http://localhost:4200** y ve al login.

3. En `frontend/src/environments/environment.ts` debe estar:

   ```ts
   apiUrl: 'http://localhost:8000/api'
   ```

4. Inicia sesión con `admin@hiitop.com` / `password`.

Si el login falla:

- Comprueba que la API esté en marcha (`php artisan serve`).
- Revisa la consola del navegador (F12) por errores de red o CORS.
- En la API, en `.env`, verifica `DB_*` y que `php artisan migrate` y `php artisan db:seed` hayan terminado sin errores.

---

## 4. Resumen rápido

| Paso | Acción |
|------|--------|
| 1 | Crear base de datos `hiitop` en MySQL |
| 2 | En la carpeta de la API: `.env` con `DB_DATABASE=hiitop`, `DB_USERNAME`, `DB_PASSWORD` |
| 3 | `php artisan migrate` y `php artisan db:seed` |
| 4 | `php artisan serve` → API en http://localhost:8000 |
| 5 | Frontend con `apiUrl: 'http://localhost:8000/api'` y `npm start` → login en http://localhost:4200 |

Con esto puedes probar la API en local y conectar la aplicación a tu base de datos.
