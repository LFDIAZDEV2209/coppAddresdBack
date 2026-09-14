# Separación de audiencias — fase 01

La suspensión del profesional revoca su sesión ERP (`aud=erp`) y conserva su
acceso paciente (`aud=app`). Los permisos globales de una cuenta dual no deben
permitir usar una sesión paciente para acceder al directorio administrativo.

## Alcance verificado

| Superficie | Regla |
| --- | --- |
| API: `Employees.*`, `Organizations.*`, `Clinics.*`, `Locations.*` | Requieren audiencia ERP además del permiso. |
| API: `Professionals.Create`, `.Update`, `.Delete` | Requieren audiencia ERP además del permiso. |
| `GET /api/v1/professionals/stats` | Requiere audiencia ERP y `Professionals.View`. |
| Telemedicina: `Appointments.AdminView`, alias `Telemedicine.AdminView` | Requieren audiencia ERP antes de evaluar claims o scopes. |
| Auth: usuarios, roles, permisos y asignaciones scoped | Ya protegen la audiencia con `RequireErpAudience`; sin cambios en esta fase. |

No se eliminan permisos de los JWT paciente. Se preservan las reglas de rutas
compartidas: `Patients.*`, `Program.*`, tests propios, catálogo de profesionales,
almacenamiento, chat y comunidad. La app consume programa, salud propia,
almacenamiento, Food AI, chat, notificaciones y GraphQL de comunidad.

Este mapa cubre el directorio y la consola administrativa indicada; no representa
una auditoría completa de separación de audiencias de todas las rutas ERP.
Los flujos compartidos requieren evaluación por acción antes de ampliar la regla.

Pruebas: `ErpDirectoryAudienceTests` y `AdminAudienceTests`, además de la integración
Auth de suspensión y conservación de sesiones paciente.
