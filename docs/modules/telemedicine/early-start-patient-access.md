# Acceso del paciente cuando la consulta se inicia antes

Una cita puede iniciarse desde el ERP antes de su horario. Antes, quedaba
`InProgress`, pero la app y el endpoint `join-token` seguían bloqueando al paciente
hasta la apertura programada.

La ventana efectiva adelanta la apertura al `StartedAt` de una sesión **activa**
persistida, únicamente para una cita `InProgress`. El cierre persistido de la
sala no se extiende. Las citas confirmadas sin sesión iniciada conservan su
ventana normal. La autorización por identidad y permisos sigue vigente.

Detalle, listado del paciente y emisión del token usan la misma regla. El
listado carga las sesiones activas de las salas en lote, sin consultas por fila.
No requiere migración y también cubre sesiones ya iniciadas antes del despliegue.

Regresión: `EarlyStartedRoomTests` verifica acceso anticipado activo, bloqueo
sin sesión activa, bloqueo de citas confirmadas y cierre sin prórroga.
