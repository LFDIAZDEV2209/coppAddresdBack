# Telemedicina — Analytics y pre-agregación CQRS

Documento del pipeline de analítica del módulo de Telemedicina (`tele.`):
tabla rollup EAV, contrato de eventos, processor, backfill y lectura del
dashboard. Referencia transversal: `docs/architecture/analytics-cqrs-preaggregation.md`.

## 1. Métricas soportadas

- **Volumen de citas**: total diario, distribución por estado y por hora
  (`daily_total`, `status_count`, `hourly_count`) — base del dashboard.
- **Métricas de llamada (F5)**: salas abiertas, sesiones iniciadas/terminadas,
  duración sumada, reaperturas y chat por rol.
- **Stats del profesional** (`tele.professional_daily_stats`): total,
  completadas, canceladas, no-show y pacientes únicos por día.

## 2. Entidad y esquema (real)

Entidad `AppointmentDailyMetric`
(`Domain/Entities/AppointmentDailyMetric.cs`), tabla
`tele.appointment_daily_metrics`:

```csharp
public sealed class AppointmentDailyMetric
{
    public DateOnly MetricDate { get; set; }
    public Guid ProfessionalId { get; set; } = Guid.Empty; // Guid.Empty = espejo global
    public Guid? ClinicId { get; set; }                    // informativa (no PK)
    public string MetricKey { get; set; } = "";
    public string DimensionKey { get; set; } = "general";
    public long TotalCount { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
```

- **PK**: `(metric_date, professional_id, metric_key, dimension_key)`.
- **Índice de lectura**: `ix_appointment_daily_metrics_lookup`
  `(metric_date, professional_id, metric_key)`.
- **Espejo global**: fila con `professional_id = Guid.Empty` agregando todas las
  clínicas/profesionales (vista admin).
- **EAV**: una métrica nueva = una `metric_key` nueva; no requiere migración de
  esquema. La clínica NO forma parte de la PK (se conserva como dato
  informativo, `MAX`), porque un profesional puede atender en dos clínicas el
  mismo día.
- La fecha de agenda es la de `scheduled_start` (UTC) de la cita, no la fecha
  real de la llamada: misma convención para processor y backfill.
- Historial de migraciones aislado en `tele.__ef_migrations_history`.

`tele.professional_daily_stats` es la tabla tipada del profesional/día
(`total_appointments`, `completed_appointments`, `cancelled_appointments`,
`no_show_appointments`, `unique_patients`).

## 3. Contrato de eventos

`Application/Features/Telemedicine/Events/ITelemedicineMetricEvent.cs`:

- `AppointmentScheduledMetricEvent` (alta de cita).
- `AppointmentStatusChangedMetricEvent` (`OldStatus→NewStatus`; de aquí se
  deriva `reopens` con `Completed→InProgress`, sin evento nuevo).
- `RoomOpenedMetricEvent` (primera apertura de sala; la reapertura no lo emite).
- `SessionStartedMetricEvent`.
- `SessionEndedMetricEvent` (`DurationSeconds` puede ser null: suma 1 a
  `sessions_ended` y 0 a la duración).
- `ChatMessageSentMetricEvent` (`Role` derivado del JWT, nunca del cuerpo).

Los emisores encolan con el parámetro opcional `ITelemedicineMetricsQueue?`
(último del constructor primario): `JoinSessionCommand`, `StartSessionCommand`,
`EndSessionCommand`, `SendRoomChatMessageCommand`, `ReopenSessionCommand`
(reutiliza el evento de estado), `StaleSessionSweeper` y
`ProcessTwilioWebhookCommand` (este último **después** del commit, para que un
duplicado con rollback no cuente). Nunca antes del `UpdateAsync`/commit.

## 4. Processor y claves (contrato compartido)

`Infrastructure/Metrics/TelemedicineMetricsProcessorHostedService.cs` consume
`ITelemedicineMetricsQueue` (Channel en memoria por réplica) y hace upsert
atómico doble (profesional + espejo `Guid.Empty`) con
`ON CONFLICT (metric_date, professional_id, metric_key, dimension_key) DO UPDATE`.

| Clave | Dimensión | Incremento |
|---|---|---|
| `daily_total` | `general` | +1 |
| `status_count` | nombre del enum `AppointmentStatus` (incrementa el nuevo, decrementa el viejo con `GREATEST(0, …)`) | +1 |
| `hourly_count` | `Hour_HH` (UTC) | +1 |
| `rooms_opened` (F5) | `general` | +1 |
| `sessions_started` (F5) | `general` | +1 |
| `sessions_ended` (F5) | `general` | +1 |
| `session_duration_seconds` (F5) | `general` | +duración (suma; promedio = suma ÷ `sessions_ended` en lectura) |
| `reopens` (F5) | `general` | +1 |
| `chat_messages_sent` (F5) | `Professional｜Patient｜Supervisor` | +1 |
| `join_tokens_issued` (P2) | rol | **solo evento** (no emitida en v1) |
| `participant_connections` (P2) | `Professional｜Patient｜Unknown` | **solo evento** (no emitida en v1) |

Cambiar una clave existente exige backfill total. Las claves P2 no son
reconstruibles desde OLTP y quedan documentadas como «solo evento».

## 5. Lectura del dashboard

`GetDashboardAnalyticsQuery` arma `DashboardAnalyticsDto` (KPIs, series,
distribuciones, actividad, próximas citas y `States`), cachea los agregados con
TTL 30–60 s con jitter y expone el bloque aditivo **`Calls`** (`CallMetricsDto`)
en los endpoints existentes:

- `GET /api/v1/telemedicine/admin/analytics?from&to` (permiso
  `Telemedicine.AdminView`).
- `GET /api/v1/telemedicine/me/analytics?from&to` (profesional por identidad).

`IAppointmentRepository.GetCallMetricsAsync` es rollup-first: si hay filas de
las claves de llamada en el rango usa el rollup; si no, cae a conteos vivos
sobre `tele.virtual_rooms`/`tele.telemedicine_sessions`/`tele.chat_messages`
(las claves P2 valen 0/empty) y nunca falla la request. El promedio de duración
se calcula en lectura (`TotalDurationSeconds ÷ SessionsEnded`; sin sesiones
terminadas → `null`).

## 6. Backfill (recálculo autoritativo)

Endpoint admin: `POST /api/v1/telemedicine/admin/analytics/backfill`
(permiso `Appointments.AdminView`). Comando
`BackfillMetricsCommand(From?, To?, ClinicId?, DryRun=false)` →
`BackfillMetricsResult`.

- **Cuándo**: carga inicial tras crear las tablas, reparación de deriva (el
  processor incremental no recupera eventos perdidos ni descuenta
  reprogramaciones entre días) y recálculo tras cambios de reglas.
- **Semántica**: agrega por día de agenda (fecha UTC de `scheduled_start`) con
  las mismas claves/dimensiones del processor y **sobrescribe**
  (`DO UPDATE SET total_count = EXCLUDED.total_count`), nunca suma. Idempotente.
- **Claves reconstruibles (F5)**: `rooms_opened` (citas con fila en
  `virtual_rooms`), `sessions_started`/`sessions_ended`/
  `session_duration_seconds` (`tele.telemedicine_sessions`), `reopens`
  (`SUM(appointments.reopen_count)` con `HAVING > 0`) y `chat_messages_sent`
  (`tele.chat_messages.sender_role`). Las claves sin datos no generan filas; las
  claves existentes que el backfill no calcula quedan intactas.
- **P2**: `join_tokens_issued`/`participant_connections` no se reconstruyen (solo
  evento).
- **Concurrencia**: si llegan eventos del processor durante la ejecución para
  días ya recalculados, el recálculo los pisa; **re-ejecutar una vez converge**.
  Correr en horario de bajo tráfico. Implementación en
  `Infrastructure/Metrics/MetricsBackfillService.cs` (SQL set-based,
  transacción explícita corta vía `CreateExecutionStrategy`; `DryRun` hace
  rollback y solo reporta).
- **Rango**: `From`/`To` opcionales (default: historia completa → hoy),
  `ClinicId` opcional, validados por FluentValidation (rango ≤ 10 años).

## 7. Limitaciones conocidas

- La cola es en memoria por réplica: eventos en vuelo se pierden en
  crash/reinicio; el backfill es la reconciliación. Las claves P2 no son
  reconstruibles.
- `participant_connections` depende de webhooks activos y accesibles.
- El promedio de duración incluye cierres por barrido (sesiones sin `EndedAt`
  propio calculan duración en `EndActiveSession`), así que una consulta
  abandonada suma su tiempo hasta el barrido.
