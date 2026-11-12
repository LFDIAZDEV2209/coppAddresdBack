# Canal WhatsApp

Módulo del monolito (`CoppAddresd.WhatsApp`), con contratos en Application y entidades
en Domain. No incorpora otro servicio desplegable. OpenSpec:
`openspec/changes/canal-whatsapp` en la raíz del workspace (decisiones D1–D9).

## Configuración

Solo variables de entorno: `WHATSAPP_ENABLED` (`0` por defecto),
`WHATSAPP_ACCESS_TOKEN`, `WHATSAPP_PHONE_NUMBER_ID`, `WHATSAPP_APP_ID`,
`WHATSAPP_APP_SECRET`, `WHATSAPP_WEBHOOK_VERIFY_TOKEN`. Las cinco son obligatorias
al habilitar el módulo. Con el flag apagado no registra servicios del canal.

## Persistencia

Schema exclusivo `whatsapp`, historial `whatsapp.__ef_migrations_history`.
El modo `--migrate` aplica este contexto incluso con el canal apagado; no necesita
credenciales de Meta. Nunca auto-migrar durante el arranque normal.

```powershell
dotnet ef database update --project src/CoppAddresd.WhatsApp --context WhatsAppDbContext
dotnet ef migrations list --project src/CoppAddresd.WhatsApp --context WhatsAppDbContext
dotnet test tests/CoppAddresd.WhatsApp.Tests
```
