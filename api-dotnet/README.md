# AdminHiitop API .NET

Base inicial de migracion desde `AdminHiitop\api` (Laravel) hacia una API .NET 8 con arquitectura limpia inspirada en `PadLockApi_v2`.

## Estado actual

- Arquitectura por capas:
  - `Controllers`
  - `Application`
  - `Domain`
  - `Infrastructure`
  - `Shared`
- `DbContext` con entidades para catalogos, inventario, ventas, facturacion y seguridad.
- Soft delete global para entidades auditables.
- Seeder inicial para catalogos base, configuracion SUNAT, series de comprobantes, roles y usuario admin.
- Integracion desacoplada para proveedor de facturacion electronica con base para `NubeFact`.
- Migracion EF Core generada:
  - `AdminHiitop.Api/Migrations/20260512213058_InitialHiitopSchema.cs`

## Endpoints listos

- `GET /api/health`
- `GET /api/settings`
- `PUT /api/settings/{key}`

## Siguientes pasos recomendados

1. Portar verticalmente los modulos de negocio:
   - autenticacion
   - productos
   - stock
   - pedidos
   - facturacion
   - resumentes diarios SUNAT
2. Reemplazar el stub de `NubeFactClient` por llamadas reales a la API de Nubefact.
3. Completar seeders grandes pendientes:
   - provincias y distritos
   - catalogos extensos de colores y tallas
4. Encender:
   - `Database:AutoMigrate`
   - `Database:AutoSeed`
   solo en ambientes controlados.
5. Agregar autenticacion JWT y autorizacion basada en permisos.

## Comandos utiles

```powershell
dotnet build .\AdminHiitop.Api\AdminHiitop.Api.csproj
dotnet ef database update --project .\AdminHiitop.Api\AdminHiitop.Api.csproj --startup-project .\AdminHiitop.Api\AdminHiitop.Api.csproj
dotnet run --project .\AdminHiitop.Api\AdminHiitop.Api.csproj
```
