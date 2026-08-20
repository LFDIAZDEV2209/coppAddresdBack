# Módulo de Telemedicina — Microservicio

## Estado

Fases implementadas:
- **Fase 0 — Scaffold**: microservicio `src/Services/CoppAddresd.Telemedicine` (puerto **5130** http / 7130 https), Clean Architecture por carpetas (precedente: Auth Service), JWT del Auth Service (mismo secret/issuer/audiences), Swagger, `/health`.
- **Fase 1 — Dominio + persistencia**: schema `tele.` con 9 tablas, migración `AddTelemedicineSchema` aplicada.
- **Fase 2 — IVideoProvider + Twilio**: contrato agnóstico en Application, `TwilioVideoProvider` en Infrastructure (SDK oficial validado contra la cuenta real), validación de firma de webhook, DI config-driven.

Pendiente: Fase 3 (requests + appointments + reglas de agendamiento), Fase 4 (salas/sesiones), Fase 5 (encuentro clínico), Fase 6 (alertas), Fase 7 (datos de referencia vía backend), Fase 8 (permisos `Telemedicine.*`), Fase 9-10 (frontend), Fase 11-12 (testing/hardening).

## Arquitectura

El microservicio es independiente del proveedor de video. El dominio de telemedicina depende de `IVideoProvider` (Application), nunca de Twilio.

```text
Controllers → MediatR (Application) → Domain
                        │
                        └→ IVideoProvider (Application)
                                │
                        TwilioVideoProvider (Infrastructure)
```

- **BD**: misma instancia PostgreSQL compartida, **solo schema `tele.`**. El historial de migraciones vive en `tele.__ef_migrations_history` (aislado del `public.__EFMigrationsHistory` del backend).
- **Datos maestros** (pacientes, profesionales, sedes, especialidades): referencias **débiles por Id** (columnas UUID indexadas, **sin FK cross-schema**). El microservicio NO posee esos datos; los leerá del backend vía internal endpoints (Fase 7).
- **Convención snake_case** vía `UseSnakeCaseNamingConvention()` (paquete `EFCore.NamingConventions`). NO usar `SetRegion` con el SDK Twilio: el host de Video es `video.twilio.com` (sin sufijo); `SetRegion("us1")` genera `video.us1.twilio.com` que no resuelve.

## Schema `tele.`

| Tabla | Notas |
|---|---|
| `telemedicine_requests` | Solicitud del paciente (referencias débiles, estado `Pending/Approved/Rejected/Cancelled/Converted`) |
| `telemedicine_appointments` | Cita (agregado raíz). `xmin` como token de concurrencia; **índice único parcial** `ix_appointments_professional_start_active` sobre `(professional_id, scheduled_start)` WHERE status IN activos → anti doble reserva en BD |
| `appointment_cancellations` | Historial append-only de cancelaciones |
| `appointment_reschedules` | Historial append-only de reprogramaciones |
| `virtual_rooms` | Sala en el proveedor (`provider`, `provider_room_sid`, `provider_room_name` único por proveedor → base de la idempotencia) |
| `telemedicine_sessions` | Sesión de video (estado independiente de la cita y de la sala) |
| `clinical_encounters` | Encuentro clínico, `clinical_data` jsonb extensible |
| `telemedicine_alerts` | Bandeja (eventos de dominio materializados; canal de entrega desacoplado) |
| `telemedicine_settings` | Reglas parametrizadas por organización/clínica |

**Estados separados a propósito**: cita ≠ sesión ≠ sala (máquinas de estado independientes).

## IVideoProvider

```csharp
CreateRoomAsync(RoomRequest, ct)          // idempotente por RoomName determinista ("apt-{id}")
GetRoomAsync(sidOrName, ct)               // null si no existe (404 mapeado)
CompleteRoomAsync(sid, ct)                // finaliza sala
GenerateAccessTokenAsync(AccessTokenRequest, ct)  // JWT identity + room, TTL corto
GetParticipantsAsync(sid, ct)
ValidateWebhookSignatureAsync(WebhookValidationRequest, ct)  // X-Twilio-Signature
```

Añadir otro proveedor = nueva clase que implemente `IVideoProvider` + un `else if` en `DependencyInjection.AddVideoProvider` (config `Telemedicine:Provider`).

## Configuración

- `Twilio` (gitignoreado): `AccountSid`, `ApiKeySid`, `ApiKeySecret` (key **región US1**), `AuthToken` (para firma de webhooks), `ValidateWebhookSignature` (`false` solo dev).
- Gotcha SDK: `TwilioClient.Init(apiKeySid, apiKeySecret, accountSid)` — el orden es (username=ApiKeySid, password=ApiKeySecret, accountSid), NO (accountSid, apiKey, secret).
- `Telemedicine:Provider` = `twilio` (default).

## Comandos

```bash
dotnet build src/Services/CoppAddresd.Telemedicine/CoppAddresd.Telemedicine.csproj
dotnet run --project src/Services/CoppAddresd.Telemedicine        # http://localhost:5130
dotnet ef migrations add <Nombre> --project src/Services/CoppAddresd.Telemedicine --startup-project src/Services/CoppAddresd.Telemedicine --output-dir Infrastructure/Migrations
dotnet ef database update --project src/Services/CoppAddresd.Telemedicine --startup-project src/Services/CoppAddresd.Telemedicine
```

> Usar siempre `--output-dir Infrastructure/Migrations`: `MigrationsDirectory` del csproj no se honró en este setup.
