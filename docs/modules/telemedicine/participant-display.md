# Identidad visible de participantes

`GET /appointments/{id}/room` agrega `displayName` y `role` opcionales a cada
participante. Compara la identidad Auth del proveedor con el usuario del paciente
y del profesional asignados. El resto se identifica como supervisión, sin
atribuirle el nombre del profesional. Resuelve dos referencias por sala, sin N+1.

ERP y app consumen estos datos. La autorización existente del endpoint no cambia.
Regresión: `RoomParticipantIdentityTests` cubre las tres identidades en una sala.
