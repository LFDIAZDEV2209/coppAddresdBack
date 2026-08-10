# Módulo de Autenticación y Autorización

Sistema completo de autenticación JWT con autorización basada en permisos granulares, implementado como servicio web standalone.

## Arquitectura

```
┌─────────────────────────────────────────────────────────────┐
│                    Auth Service (Puerto 5058)                 │
├─────────────────────────────────────────────────────────────┤
│  Controllers                                                │
│  ├── AuthController      → Login, Refresh, Logout, Password │
│  ├── MeController        → Info usuario actual              │
│  ├── UsersController     → CRUD usuarios                    │
│  ├── RolesController     → CRUD roles + asignación          │
│  └── PermissionsController → CRUD permisos + asignación     │
├─────────────────────────────────────────────────────────────┤
│  Services                                                   │
│  ├── AuthService         → Lógica de autenticación          │
│  ├── UserService         → Lógica de usuarios               │
│  ├── RoleService         → Lógica de roles                  │
│  ├── PermissionService   → Lógica de permisos               │
│  └── TokenService        → Generación JWT + Refresh         │
├─────────────────────────────────────────────────────────────┤
│  Authorization                                              │
│  ├── PermissionPolicyProvider  → Dynamic policy provider    │
│  ├── PermissionHandler       → Verifica permisos en DB      │
│  └── RequirePermissionAttribute → [RequirePermission("X")]  │
├─────────────────────────────────────────────────────────────┤
│  Security                                                   │
│  ├── TokenInvalidationService → Invalida via SecurityStamp  │
│  └── SecurityStampValidator   → Valida stamp en cada request│
├─────────────────────────────────────────────────────────────┤
│  Data                                                       │
│  ├── AuthDbContext           → IdentityDbContext<Guid>      │
│  └── Schema: auth.           → 12 tablas                    │
└─────────────────────────────────────────────────────────────┘
```

## Decisiones de diseño

| Decisión | Razón |
|----------|-------|
| **Servicio standalone** | Auth no depende de otros proyectos. Puede desplegarse independientemente. |
| **Permisos granulares** | `Users.View`, `Users.Create`, etc. Más flexible que solo roles. |
| **Permisos directos + via rol** | Usuario puede tener permisos directos (excepciones) o via rol. |
| **Refresh tokens en DB** | Rotación automática, revocación en logout/cambio password. |
| **SecurityStamp invalidation** | Cambios de password/rol/permiso invalidan tokens inmediatamente. |
| **Schema `auth.` separado** | Consistencia con `audit.`. Previene contaminación de `public.` |

## Modelo de datos (Schema `auth.`)

```sql
auth.Users                    → Usuarios (Identity)
auth.Roles                    → Roles (Identity)
auth.UserRoles                → Asignación usuario-rol (Identity)
auth.UserRoleAssignments      → Asignación con navegación (custom)
auth.Permissions              → Permisos granulares (custom)
auth.RolePermissions          → Permisos de rol (custom)
auth.UserPermissions          → Permisos directos de usuario (custom)
auth.RefreshTokens            → Refresh tokens con rotación (custom)
auth.UserClaims               → Claims de usuario (Identity)
auth.RoleClaims               → Claims de rol (Identity)
auth.UserLogins               → Logins externos (Identity)
auth.UserTokens               → Tokens 2FA (Identity)
```

## Flujo de autenticación

### Login
```
1. POST /api/auth/login (email + password)
2. Validar credenciales con UserManager
3. Si falla → incrementar lockout counter
4. Si OK → generar AccessToken (JWT) + RefreshToken (DB)
5. Retornar tokens + info usuario
```

### Request autorizado
```
1. Request con Header: Authorization: Bearer <token>
2. JwtBearer valida firma, issuer, audience, expiración
3. SecurityStampValidator verifica stamp contra DB
4. PermissionHandler verifica permisos (directos + via rol)
5. Controller ejecuta acción
```

### Refresh token
```
1. POST /api/auth/refresh (refreshToken)
2. Buscar token en DB
3. Validar: no expirado, no revocado
4. Generar nuevo AccessToken + nuevo RefreshToken
5. Marcar token viejo como reemplazado (ReplacedByTokenId)
6. Retornar nuevos tokens
```

### Invalidación de tokens
```
Cambio de password/rol/permiso →
  UserManager.UpdateSecurityStampAsync() →
  SecurityStamp en DB cambia →
  SecurityStampValidator detecta mismatch →
  Token rechazado →
  Usuario debe hacer login de nuevo
```

## Permisos

### Módulo Users
- `Users.View` — Listar/obtener usuarios
- `Users.Create` — Crear usuarios
- `Users.Update` — Actualizar usuarios
- `Users.Delete` — Eliminar usuarios

### Módulo Roles
- `Roles.View` — Listar/obtener roles
- `Roles.Create` — Crear roles
- `Roles.Update` — Actualizar roles
- `Roles.Delete` — Eliminar roles
- `Roles.Assign` — Asignar/quitar roles a usuarios

### Módulo Permissions
- `Permissions.View` — Listar/obtener permisos
- `Permissions.Assign` — Asignar/quitar permisos a roles/usuarios

### Módulo Agents
- `Agents.View` — Ver agentes IA
- `Agents.Create` — Crear agentes
- `Agents.Update` — Actualizar agentes
- `Agents.Delete` — Eliminar agentes

## Configuración

```json
{
  "Jwt": {
    "Secret": "change-this-to-a-secure-secret-key-at-least-32-chars-long",
    "Issuer": "CoppAddresd.Auth",
    "Audience": "CoppAddresd.Clients",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "Auth": {
    "AdminEmail": "admin@coppaddresd.com",
    "AdminPassword": "Test@1234",
    "AdminFirstName": "Admin",
    "AdminLastName": "System"
  }
}
```

**Importante**: JWT debe ser idéntico entre Auth Service y API para que los tokens funcionen.

## Startup

Al iniciar, Auth Service automáticamente:
1. Aplica migraciones pendientes
2. Seedea 15 permisos (idempotente)
3. Crea rol "Admin" si no existe
4. Crea usuario admin con todos los permisos

## Rate limiting

- 100 requests/minuto por IP
- Configurado en `Program.cs` con `AddRateLimiter()`

## Health checks

```
GET /health → 200 OK si DB está accesible
```

Configurado con `HealthChecks.NpgSql`.

## Seguridad

- **Password policy**: 8+ chars, mayúscula, minúscula, número, especial
- **Lockout**: 15 minutos después de 5 intentos fallidos
- **Refresh token rotation**: Cada refresh genera nuevo token, invalida el anterior
- **SecurityStamp**: Cambios críticos invalidan todos los tokens activos
- **CORS**: AllowAll (configurar en producción)
- **HTTPS**: Requiere en producción (RequireHttpsMetadata = false solo en dev)

## Testing

```bash
# Login
curl -X POST http://localhost:5058/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@coppaddresd.com","password":"Test@1234"}'

# Usar token
curl http://localhost:5058/api/me \
  -H "Authorization: Bearer <accessToken>"

# Refresh
curl -X POST http://localhost:5058/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refreshToken>"}'
```

## TODO / Mejoras futuras

- [ ] Integrar `HttpAuditActorContext` con Identity para capturar userId/email en auditoría
- [ ] Agregar 2FA (TwoFactorEnabled ya está en schema)
- [ ] Logins externos (Google, GitHub) — `UserLogins` ya existe
- [ ] Email confirmation (RequireConfirmedEmail = false actualmente)
- [ ] Frontend para gestión de usuarios/roles/permisos
- [ ] Configurar CORS específico por dominio en producción
- [ ] HTTPS obligatorio en producción
