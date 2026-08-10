---
name: dotnet
description: 'Convenciones .NET 10 / C# 13 del proyecto: comandos, formato de solución, paquetes, gotchas de tooling.'
---

# .NET / C# — Convenciones del proyecto

## Versiones

- .NET 10 / C# 13 (`net10.0`). Verificar: `dotnet --version` (necesita 10.0+).
- Paquetes clave: EF Core 10.0.10, Npgsql 10.0.3, MediatR 14.2.0, FluentValidation 12.1.1, Identity/JwtBearer 10.0.10, OpenApi 10.0.5.

## Comandos

```bash
dotnet build                                    # compilar solución
dotnet test                                     # todas las pruebas
dotnet test tests/CoppAddresd.UnitTests         # unitarias
dotnet test tests/CoppAddresd.IntegrationTests  # integración
dotnet test --filter "FullyQualifiedName~X"     # una prueba
dotnet run --project src/CoppAddresd.Api        # API (5122 / 7258)
dotnet run --project src/Services/CoppAddresd.Auth  # Auth (5058 / 7230)
dotnet ef migrations add Nombre --project src/CoppAddresd.Infrastructure --startup-project src/CoppAddresd.Api
```

No hay script de lint/format. **El build debe pasar antes de dar por terminada una tarea.**

## Gotchas de tooling

- **`CoppAddresd.slnx`** (formato XML nuevo): no existe `.sln`. Herramientas que esperen `.sln` fallarán. No crear `.sln`.
- **`appsettings.json` / `appsettings.*.json` están gitignoreados** en Api y Auth: hay que crearlos localmente para ejecutar. No hay plantilla `appsettings.Example.json` versionada todavía — y el patrón `appsettings.*.json` del .gitignore la ignoraría: para versionarla habría que añadir `!appsettings.Example.json`.
- Auth: servicio standalone sin referencias a proyectos del repo — mantenerlo así.
- IDE: JetBrains (`.idea/` presente), VS, VS Code.

## Estilo de código

- `Nullable` + `ImplicitUsings` habilitados (definidos en cada csproj).
- Records para DTOs e inmutables; Minimal APIs (no controllers) en la capa HTTP.
- Comentarios/doc en español.
- `async`/`await` de punta a punta; nunca `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, `Task.Run` para E/S, `async void` (excepto event handlers).
- Nombrar métodos async con sufijo `Async`.

## Verificación de calidad

- Tests con xUnit (ver skill `testing`).
- Revisar `README.md` como fuente de verdad de arquitectura/stack; actualizar si cambia el stack.
