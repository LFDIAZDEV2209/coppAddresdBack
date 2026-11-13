# Fase 0 — evidencia del backend

Rama `org/f0-coppAddresdBack`, creada desde
`a105ba7c89c8d604e0946b6f12a779d27e871f04`. Commits locales por tarea;
sin push, integración, rebase ni despliegue. `tasks.md` lo mantiene coordinación.

| Tarea | Aceptación verificada |
| --- | --- |
| 0.11a | Inventario de 199 rutas, recursos indirectos y vías de firma; copia en el change y `AUDITORIA-ERP-ALCANCE.md` |
| 0.10a | Matriz §10.1 en seed, reconciliación idempotente de concesiones antiguas |
| 0.10b | 52 casos independientes rol × operación; permisos ejercidos con autorización real |
| 0.11b | Guard: propio, ajeno, Admin, permiso obligatorio, cancelación y consultas filtradas |
| 0.11c | Cada ruta anotada prueba propio/ajeno/Admin/sin permiso; masivo mixto no despacha al ajeno |
| 0.11d | HTTP con permisos efectivos, lectura separada, fotos/notas; consulta real PostgreSQL de bandeja, totales, listas, ciclos y análisis ajenos |
| 0.9 | Handler: nombre, zona local, ciclos sin duplicar versiones, futuro/vencido/null; cabeceras PostgreSQL solo del paciente |

Verificación final: `dotnet build` correcto; unitarios: **2365 aprobados, 105 omitidos**
por condiciones preexistentes de la suite. Nutrición: **48 integraciones existentes
aprobadas**, más las dos nuevas comprobaciones PostgreSQL aprobadas individualmente.
No se llamó a proveedores reales de IA ni a producción.

La suite general de integración no está completamente verde con una copia de esquema
local sin datos: reproduce nueve fallos que también aparecen en `a105ba7`, comprobado
en un worktree temporal separado. No se corrigieron módulos ajenos a esta fase:

- Cuatro casos de `PatientDashboardIntegrationTests`.
- Dos casos de `DashboardKpisFallbackIntegrationTests`.
- Tres casos de `ProgramAssignmentIntegrationTests`.

Las bases de pruebas fueron desechables. La conexión se resolvió en memoria y no se
guardó en archivos de trabajo. Un primer intento de lectura JSON falló y volcó
configuración privada en una salida de herramienta; se notificó a Luis y se corrigió
la lectura para evitar repetir valores o detalles privados. Deben revisarse las
credenciales afectadas; no se rotaron ni modificaron secretos.

Fetch de coordinación: `origin/dev` avanzó a `671349c`, incluyendo `392439a`
(publicados primero en bandeja). Se conserva el ORDER BY de la base solicitada;
la integración deberá conservar esa corrección remota junto con el guard. Sin pull
ni rebase de esta rama.

Postman CLI instalado: `postman status` no está soportado por esa versión.
La evidencia ejecutable de esta fase son las suites .NET; no se creó ni publicó
un workspace Postman como parte de este trabajo.
