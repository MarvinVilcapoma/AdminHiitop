# Hiitop con Docker

**Docker** es gratuito para uso personal y para empresas pequeñas (Docker Desktop). Aquí se usa para levantar la API (api-hiitop), el frontend (front-hiitop) y la base de datos MySQL (hiitop-db).

## Servicios

| Servicio      | Contenedor        | Puerto (host) | Descripción        |
|---------------|-------------------|---------------|--------------------|
| api-hiitop    | api_hiitop_app    | —             | Laravel PHP-FPM   |
| nginx-hiitop  | api_hiitop_nginx  | **8051**      | Acceso HTTP a la API (si 8050 está ocupado, en `docker-compose.yml` cambia a otro, p. ej. 8051) |
| hiitop-db     | hiitop_db         | **3310**      | MySQL 8, base `hiitop` (3310 para no chocar con MySQL local en 3306) |
| front-hiitop  | front_hiitop_app  | **4200**      | Angular (ng serve) |

## Uso rápido

1. **Crear `.env` de la API** (para Docker):
   ```bash
   cd api
   copy .env.docker.example .env
   cd ..
   ```

2. **Levantar todo**:
   ```bash
   docker compose up -d
   ```

3. **Generar APP_KEY y migrar** (solo la primera vez):
   ```bash
   docker compose exec api-hiitop php artisan key:generate
   docker compose exec api-hiitop php artisan migrate --force
   docker compose exec api-hiitop php artisan db:seed --force
   ```

4. **URLs** (con los puertos actuales del `docker-compose.yml`):
   - Frontend: http://localhost:4200  
   - API: http://localhost:8051 (ej.: http://localhost:8051/api/login)

5. **Conectar el frontend a la API con Docker**  
   En `frontend/src/environments/environment.ts` pon el mismo puerto que use nginx (p. ej. 8051):
   ```ts
   apiUrl: 'http://localhost:8051/api'
   ```
   Así el navegador llama a la API que expone nginx.

## Conectar con un cliente MySQL (DBeaver, DataGrip, etc.)

Las migraciones se ejecutaron **dentro de Docker**, contra el contenedor **hiitop-db**. Para ver la base de datos desde tu PC usa:

| Campo      | Valor        |
|-----------|--------------|
| **Host**  | `127.0.0.1`  |
| **Puerto**| **`3310`**   |
| **Base de datos** | `hiitop` |
| **Usuario** | `root`     |
| **Contraseña** | **`root_secret`** |

Alternativa con usuario dedicado: usuario `hiitop`, contraseña `hiitop_secret`.

Si dejas la contraseña vacía obtendrás *"Access denied for user 'root'@'localhost' (using password: NO)"*: el MySQL del contenedor **siempre** exige contraseña (root: `root_secret`).

**"Public Key Retrieval is not allowed"**: El contenedor MySQL está configurado con `--default-authentication-plugin=mysql_native_password` para evitar este error con MySQL 8. Si usas un cliente JDBC (DBeaver, DataGrip, etc.) y aún lo ves, en la URL de conexión añade `?allowPublicKeyRetrieval=true` o marca la opción equivalente en las propiedades del driver. Tras cambiar el `command` de MySQL, puede ser necesario recrear el contenedor: `docker compose up -d --force-recreate hiitop-db`.

## Notas

- La base de datos se llama **hiitop**; usuario `hiitop`, contraseña `hiitop_secret` (root: `root_secret`).
- Para que `php artisan` funcione dentro del contenedor, la carpeta `api` debe ser un **proyecto Laravel completo** (con `artisan`, `vendor` instalado por Composer, etc.). Si solo tienes el código de Hiitop (modelos, rutas, migraciones), sigue **SETUP.md** para crear/instalar Laravel y luego vuelve a levantar Docker.
- Para ver logs: `docker compose logs -f api-hiitop` o `docker compose logs -f front-hiitop`.
- Para parar: `docker compose down`. Los datos de MySQL se guardan en el volumen `hiitop_db_data`.
