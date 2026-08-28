# Módulo: Caché distribuido con Valkey

Estado: implementado y validado en desarrollo (change `valkey-distributed-cache`). Este documento es la referencia para **levantar el entorno**, **entender qué se cachea** y **migrar a AWS**.

## Arquitectura

```
ERP (Next.js) / Mobile ──► Gateway ──► Servicios .NET ──► Valkey (caché) ──► PostgreSQL
                                          │
                                          └─ fail-open: Valkey caído ⇒ PostgreSQL directo
```

- **Un solo backend de caché** (Valkey) compartido por los tres servicios .NET, con claves namespaced por servicio: `erp:*`, `auth:*`, `tele:*`. En producción el mismo binario apunta a **ElastiCache for Valkey** sin cambios de código.
- **Abstracción por servicio** (Auth y Tele son standalone y no referencian proyectos): `ICacheService` + implementaciones `ValkeyCacheService` / `MemoryCacheService` / `NoCacheService`.
  - Backend ERP: `src/CoppAddresd.Application/Interfaces/ICacheService.cs` + `src/CoppAddresd.Infrastructure/Cache/`
  - Auth: `src/Services/CoppAddresd.Auth/Services/Cache/`
  - Tele: `src/Services/CoppAddresd.Telemedicine/Infrastructure/Cache/`
- **Fail-open por operación**: cualquier fallo de Valkey (conexión, timeout, serialización) se registra como Warning y la operación se comporta como miss. El servicio NUNCA se cae por el caché; `/health` reporta el componente `valkey` como **Degraded** (HTTP 200), no tumba el endpoint.
- **TTL obligatorio en toda clave** (no existen claves sin expiración) + `--maxmemory-policy volatile-lru`: el eviction solo toca claves con TTL (caché), nunca claves estructurales.

## Levantar el entorno

```powershell
# 1. Desde la raíz del workspace (donde vive docker-compose.yaml)
docker compose up -d          # PostgreSQL (coppAddresd) + Valkey (coppAddresd-valkey)

# 2. Verificar ambos
docker compose ps             # ambos deben estar (healthy)
docker exec coppAddresd-valkey valkey-cli -a CoppAddresdValkey2026 ping   # → PONG

# 3. Lanzar los servicios (normalmente ya lo hace tu flujo habitual)
#    Auth (5123) → API (5122) → Telemedicina (5130). Todos leen su
#    appsettings.Development.json con Cache:Provider=Valkey.
```

⚠️ **El volumen de PostgreSQL está pinnado** (`name: repos_pgdata` en el compose): ahí viven todos los datos. **Nunca** ejecutar `docker compose down -v`, `docker volume rm repos_pgdata` ni cambiar el `name:` del volumen. Los datos de Valkey viven aparte (`coppaddresd-valkeydata`) y pueden recrearse sin riesgo (el caché se reconstruye solo).

Variables del compose (opcionales):

| Variable          | Default                 | Uso                                                                                                  |
| ----------------- | ----------------------- | ---------------------------------------------------------------------------------------------------- |
| `VALKEY_PASSWORD` | `CoppAddresdValkey2026` | AUTH de Valkey local. Sobrescribir con `$env:VALKEY_PASSWORD="..."` antes de `docker compose up -d`. |

Configuración por servicio (appsettings / env):

| Clave                      | Ejemplo dev                                     | Producción (AWS)                          |
| -------------------------- | ----------------------------------------------- | ----------------------------------------- |
| `Cache:Provider`           | `Valkey`                                        | `Valkey` (o `None` para rollback)         |
| `Cache:KeyPrefix`          | `erp` / `auth` / `tele`                         | igual                                     |
| `ConnectionStrings:Valkey` | `127.0.0.1:6379,password=CoppAddresdValkey2026` | `endpoint:6379,password=<token>,ssl=true` |

En contenedores/vars de entorno: `CACHE__PROVIDER`, `CACHE__KEY_PREFIX`, `CONNECTIONSTRINGS__VALKEY`.
Nota: usar `127.0.0.1`, no `localhost` (evita que el cliente resuelva primero a IPv6 `::1`, que no está mapeada en el compose).

## Qué se cachea y qué no

| Caso                                                                                                                                              | Key (tras el prefijo)                             | TTL              | Invalidación                                                                                  | Por qué                                                                                                   |
| ------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- | ---------------- | --------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Catálogos ERP (blood types, document types, ethnicities, countries, states por país, insurers, professional-types, specialties, document-catalog) | `catalog:<nombre>[:<scopeId>]:v1`                 | 1 h              | `Remove` en el handler de la mutación (professional-types/specialties); el resto es seed-only | Lectura masiva, escritura rara                                                                            |
| Stats de dashboards (patients/stats, professionals/stats)                                                                                         | `stats:<nombre>:<sha256(alcance)>:v1`             | 30-60 s (jitter) | TTL (staleness máximo = TTL, tolerado)                                                        | Agregados costosos sobre ~50k pacientes; el jitter evita que las claves expiren sincronizadas (stampede)  |
| Introspección de permisos (clientes ERP y Tele → Auth)                                                                                            | `scope:<sha256(user\|stamp\|permiso\|scopes)>:v1` | 15 min           | Rotación del security stamp (cambia la clave)                                                 | Hot path entre servicios; compartido entre réplicas: con N réplicas solo la primera llama al Auth Service |
| Auth: código de permiso → id                                                                                                                      | `permits:code:<código>:v1`                        | 24 h             | No requiere (catálogo inmutable en runtime)                                                   | Se evalúa en cada authorize                                                                               |
| Auth: códigos de permisos por rol                                                                                                                 | `roles:<roleId>:codes:v1`                         | 15 min           | `Remove` en AssignToRole/RemoveFromRole                                                       | Cambia solo cuando un admin edita el rol; TTL alineado con la vida del access token                       |
| Tele: referencias del backend (profesional/paciente/especialidad/sede por cita)                                                                   | `ref:<recurso>:v1`                                | 10 min           | TTL                                                                                           | Pantallas de agenda/salas repiten las mismas referencias                                                  |

**NO se cachea (decisión deliberada):**

- Tokens, refresh tokens, códigos OTP, contraseñas — seguridad.
- Perfiles de pacientes / encuentros clínicos (PHI a nivel fila) — privacidad y frescura clínica.
- Listados paginados con filtros (`/patients`, directorio de profesionales) — baja tasa de re-petición; el beneficio real está en índices/paginación.
- Consultas de autorización por usuario dentro de Auth (asignaciones, overrides, globales) — la revocación debe ser inmediata; lo agregado (por rol/catálogo) sí se cachea.
- Health-tests `/stats` — frescura de alertas clínicas manda.

Para **agregar un caso nuevo**: usar `cache.GetOrCreateAsync(CacheKeys.X, ttl, factory, ct)` en el handler (nunca en el controller), clave versionada `:v1`, y `RemoveAsync` en la misma transacción lógica de escritura si el dato puede mutar. Bump de versión (`:v1` → `:v2`) para invalidar un dominio entero.

## Verificar Valkey

```bash
# Ping y configuración efectiva
docker exec coppAddresd-valkey valkey-cli -a CoppAddresdValkey2026 ping
docker exec coppAddresd-valkey valkey-cli -a CoppAddresdValkey2026 config get requirepass appendonly maxmemory maxmemory-policy

# Claves existentes (namespaced por servicio)
docker exec coppAddresd-valkey valkey-cli -a CoppAddresdValkey2026 keys 'erp:*'
docker exec coppAddresd-valkey valkey-cli -a CoppAddresdValkey2026 keys 'auth:*'

# Hit rate, misses y evictions (detectar stampede/pressure)
docker exec coppAddresd-valkey valkey-cli -a CoppAddresdValkey2026 info stats | grep -E 'keyspace_hits|keyspace_misses|evicted_keys'

# Memoria usada vs tope 256mb
docker exec coppAddresd-valkey valkey-cli -a CoppAddresdValkey2026 info memory | grep -E 'used_memory_human|maxmemory_human'

# Contenido legible de una clave (JSON)
docker exec coppAddresd-valkey valkey-cli -a CoppAddresdValkey2026 --no-raw get erp:catalog:blood-types:v1
```

Los servicios loguean `Cache HIT/MISS {Key}` (Debug) y `Fallo de caché en GET/SET/REMOVE` (Warning) con correlation ID.

## Pruebas automatizadas

- **Unit (siempre corren)**: semántica hit/miss/TTL/invalidación con fakes; fail-open con endpoint inaccesible; scoping de stats (claves distintas por alcance). `dotnet test tests/CoppAddresd.UnitTests` + `dotnet test tests/CoppAddresd.Telemedicine.UnitTests`.
- **Valkey real (auto-skip si no hay servidor)**: `ValkeyCacheServiceLocalValkeyTests` conecta a `127.0.0.1:6379` (o `COP_VALKEY_TEST_CONNECTION`) y verifica roundtrip/expiración/GetOrCreate contra el contenedor.

## Fail-safe (qué pasa si Valkey se cae)

1. Cada operación de caché falla con timeout ≤ 2 s (connect ≤ 5 s), se registra Warning y la operación degrada a PostgreSQL — **la petición sigue siendo correcta**.
2. `/health` reporta `Degraded` (componente `valkey`), HTTP 200 — el servicio NO se marca caído.
3. `docker compose start valkey` → el cliente se reconecta solo y el caché se reabre sin reiniciar servicios.
4. No hay avalancha: stats con TTL jitter y reconstrucción gradual; AOF (`appendfsync everysec`) arranca el caché tibio tras un reinicio del contenedor.

Verificado en local: catálogo 173 ms (miss) → 33 ms (hit); stats 150 ms → 6 ms; con Valkey detenido catálogo/stats/login responden correctamente y `/health` = `Degraded`.

## Migración a AWS ElastiCache for Valkey

La configuración local es conceptualmente idéntica a producción: **una instancia con AUTH y TTL-based eviction**. Pasos:

1. **Crear el cluster**: ElastiCache for Valkey 8.1 (misma versión mayor que local). Para empezar: nodo `cache.t4g.small` en una sola AZ con Multi-AZ habilitado (o serverless si el patrón de tráfico lo justifica). Security Group: solo entrada 6379 desde los SG de los servicios ECS (API/Auth/Tele) — nunca `0.0.0.0/0`.
2. **AUTH token**: crear en Secrets Manager (`coppaddresd/prod/valkey-auth-token`); en tránsito exige TLS → `ssl=true` en la connection string.
3. **Variables de entorno por servicio** (task def de ECS):
   ```
   CACHE__PROVIDER=Valkey
   CONNECTIONSTRINGS__VALKEY=<endpoint>:6379,password=<token-de-Secrets-Manager>,ssl=true
   ```
   Los tres servicios usan la MISMA pieza de configuración; el código y la imagen no cambian.
4. **Grupos de seguridad / IAM**: la tarea necesita `secretsmanager:GetSecretValue` sobre el token (la conexión usa el secret como password; no se imprime en logs).
5. **Verificación post-deploy**: `GET /health` (componente `valkey` Healthy), primer request de catálogo (miss) + segundo (hit), y `info stats` desde la consola de ElastiCache (hit rate creciente).
6. **Rollback**: `CACHE__PROVIDER=None` en cualquier servicio → sin caché, todo a PostgreSQL, sin redeploy ni pérdida de datos.

Backups: el caché es reconstruible por diseño (todas las claves con TTL); los snapshots de ElastiCache son conveniencia, no requisito de integridad.
