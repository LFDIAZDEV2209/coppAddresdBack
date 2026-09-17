# Configuración del avatar

Actualización Fase 14 (14/09/2026): `AvatarCatalog` acepta `female-hair-long-01`, `female-hair-long-02` y `female-hair-long-03` exclusivamente para femenino. Se conserva la validación central por slot/género; no cambian DTO, esquema o endpoints. Diez unitarias y la prueba HTTP/PostgreSQL con rollback pasaron. Login/GET/PUT compatibles funcionan en el circuito normal, pero el proceso activo aún rechaza cabello largo: pendiente cargar el binario actualizado. No se reinició Auth porque sus seeders restablecen contraseña admin y gestionan roles ajenos al avatar. El SELECT actual muestra la migración ERP ya aplicada por un cambio externo a esta tarea; no se ejecutaron migraciones. Detalle: `antares-paciente/docs/avatar-final-validation/phase14.md` en el monorepo. El texto siguiente conserva el contexto histórico de Fase 13.

Estado confirmado por el usuario: **IMPLEMENTADA, PENDIENTE DE VALIDACIÓN DEL CIRCUITO NORMAL DE LOGIN/GATEWAY**. No autorizada AddErpApplicationSuspension ni reinicios que la apliquen. Se mantiene el estado del código/BD. Diagnóstico detallado: `antares-paciente/docs/avatar-phases-11-13-validation/login-gateway-diagnosis.md` en el monorepo. Las comprobaciones nuevas fueron de solo lectura; no se implementó una alternativa.

Fase 13, 14/09/2026. Preferencias estéticas del usuario autenticado. Los archivos 3D y el estado derivado del peso permanecen fuera de la BD de configuración.

## API

- `GET /api/auth/me/avatar`: configuración guardada; si no existe, defaults compatibles con `app.patient_profiles.gender` (lectura por `user_id` del JWT). Sin perfil/género reconocido: masculino. Cabello lateral y camiseta inicial; accesorios vacíos.
- `PUT /api/auth/me/avatar`: reemplazo completo de la configuración. Devuelve la selección guardada.
- Ambos exigen JWT. No reciben un identificador de usuario en ruta o DTO. Un query `userId` no modifica el alcance. Campos desconocidos, incluidos `userId`, `body` o `weight`, se rechazan en el JSON. Respuestas privadas con `Cache-Control: no-store`.
- Contrato v1: `version`, `gender`, `hair`, `clothing: {shirt,pants,shoes}`, `accessories: {glasses,watch,bracelet}`. Los slots sin selección usan `null`.
- IDs válidos: camiseta `shirt-basic-01`/`shirt-basic-01-navy`, cabello `hair-02` para ambos y `hair-03` solo masculino; `glasses-01`, `watch-01`, `bracelet-01`. Pants/shoes solo null hasta contar con assets validados. Sin rutas o binarios enviados por cliente.
- 200 éxito; 400 contrato/compatibilidad inválida; 401 no autenticado; límite de request 4096 bytes. Los errores de infraestructura no se convierten en defaults.

## Arquitectura y almacenamiento

Se reutiliza `auth."UserPreferences"`, agregando `AvatarConfiguration jsonb NULL`. Conserva `Lang` y `AccentColor`; no se crea tabla de avatar. Migración EF `20260914152543_AddAvatarConfigurationPreference`, snapshot incluido. Up agrega solo una columna nullable; Down elimina solo esa columna. No requiere backfill ni índice: lecturas/escrituras usan la PK `UserId` existente. El Down no debe ejecutarse tras guardar preferencias sin respaldo.

Auth es un servicio standalone, dueño de esta entidad. Separación por carpetas dentro del mismo servicio: Controller → Application/Avatar (GET/PUT, interfaz del store) → Domain/Avatar (compatibilidad/defaults) → Infrastructure/Avatar (EF/PostgreSQL). Sin referencias nuevas entre proyectos. El namespace de Application se denomina `CoppAddresd.Auth.Avatar.Application` para evitar colisión con la entidad existente `Application`.

Upsert parametrizado `INSERT ... ON CONFLICT` atómico; actualiza solo configuración y fecha. No pisa idioma/acento. Último guardado completado gana entre dispositivos. CancellationToken propagado. Sin caché compartida de configuraciones.

## Validación y entorno local

- 7 pruebas unitarias de contrato, defaults, identidad, cancelación y rechazo de IDs/campos clínicos.
- Prueba HTTP real del controller con TestServer, JWT firmado y PostgreSQL: 401, 200, 400, IDOR, lectura posterior y conservación de idioma/acento. Las identidades de esa prueba viven en una transacción que se revierte.
- Prueba opt-in de durabilidad: cuenta de pruebas autorizada, configuración femenina con camiseta, cabello y tres accesorios; commit y lectura desde otra conexión. No se modificaron mediciones. Respaldo de configuración anterior en el informe frontend (`prior-demo-avatar.json`, era null).
- Ejecutar pruebas PostgreSQL con `COP_TEST_DB_CONNECTION`. La prueba de demostración solo escribe si también existe `COP_AVATAR_DEMO_DOCUMENT`; no configurar esta variable en CI general. `COP_AVATAR_RECOVERY_FILE` permite guardar el valor previo una sola vez.

La BD local tenía pendiente **otra** migración: `20260913184501_AddErpApplicationSuspension`. No se aplicó porque corresponde a ERP y está fuera de esta misión. Se revisó y ejecutó únicamente el SQL de la columna avatar y su entrada de historial (ver `migration.sql`). El API del avatar se validó con TestServer; **no se arrancó el host Auth normal ni se validó el circuito de login/gateway**, cuyo arranque ejecutaría también esa migración previa y sus seeders. Antes de reiniciar/desplegar ese host, resolver el prerrequisito ERP por el flujo correspondiente. La migración del avatar es aditiva e independiente de sus columnas.

Sin nuevas dependencias, cambios en API clínica, Next.js/admin ni tablas clínicas.
