# Priorización de plazas - Hospital del Mar (base)

## Qué incluye
- ASP.NET Core Razor Pages (login: correo + código)
- Priorización con drag & drop (actualiza el orden visual y el payload a guardar)
- Botones:
  - **Guardar**: persiste `orden_usuario` solo si la convocatoria está abierta y no está enviada
  - **Reiniciar**: pone `orden_usuario = NULL` (vuelve a orden por defecto)
  - **Enviar**: marca `enviado_en` y revoca los tokens (no se puede volver a entrar)
- PostgreSQL en Docker con inicialización automática del esquema (DDL)
- Panel **admin** (`/Admin`) protegido con Entra ID (OIDC)

## Arranque (Docker)
1. Edita `docker-compose.yml` y configura `AdminEntra__ClientSecret`.
2. Levanta el stack:

```bash
docker compose up -d
```

3. Abre en el navegador:
- http://localhost:8080

## Base de datos
El DDL se ejecuta automáticamente al crear el contenedor por primera vez desde:
- `db/init/01_schema.sql`

### Conectar de forma interactiva
```bash
docker exec -it hdm-postgres psql -U priorizacion_user -d priorizacion
```

## Variables de entorno (Azure)
En App Service / Container Apps, define:
- `ConnectionStrings__Default`
- `Auth__SessionCookieName`
- `AdminEntra__Instance` (ej. `https://login.microsoftonline.com/`)
- `AdminEntra__TenantId`
- `AdminEntra__ClientId`
- `AdminEntra__ClientSecret`
- `AdminEntra__CallbackPath` (ej. `/admin/signin-oidc`)
- `AdminEntra__SignedOutCallbackPath` (ej. `/admin/signout-callback-oidc`)

Ejemplo:
`Host=postgres;Port=5432;Database=priorizacion;Username=priorizacion_user;Password=priorizacion_pass`

## Próxima fase (panel admin)
El panel admin debe cubrir:
- gestionar ventanas de acceso (`acceso_desde`, `acceso_hasta`)
- alta de aspirantes / plazas / asignaciones (import CSV/Excel)
- generación y revocación de tokens
- exportación `vw_priorizacion_lista`
