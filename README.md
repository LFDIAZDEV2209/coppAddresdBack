<div align="center">

# 🏗️ CoppAddresd Backend

**API backend empresarial basada en .NET 10 y Clean Architecture**

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-13-239120?style=for-the-badge&logo=csharp&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)
![Entity Framework](https://img.shields.io/badge/EF%20Core-10-512BD4?style=for-the-badge&logo=entity-framework&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

</div>

---

## 📑 Tabla de contenidos

- [Descripción](#-descripción)
- [Características](#-características)
- [Stack tecnológico](#-stack-tecnológico)
- [Arquitectura](#-arquitectura)
- [Estructura del proyecto](#-estructura-del-proyecto)
- [Requisitos previos](#-requisitos-previos)
- [Configuración](#-configuración)
- [Puesta en marcha](#-puesta-en-marcha)
- [Documentación de la API (OpenAPI)](#-documentación-de-la-api-openapi)
- [Testing](#-testing)
- [Servicio de autenticación](#-servicio-de-autenticación)
- [Convenciones del proyecto](#-convenciones-del-proyecto)
- [Próximos pasos](#-próximos-pasos)
- [Contribución](#-contribución)
- [Licencia](#-licencia)

---

## 🚀 Descripción

**CoppAddresd Backend** es la API backend del ecosistema CoppAddresd. Está construida sobre **.NET 10** siguiendo los principios de **Clean Architecture**, lo que garantiza una clara separación de responsabilidades, mantenibilidad a largo plazo y testabilidad.

El repositorio contiene:

| Proyecto | Rol |
|---|---|
| **API principal** (`CoppAddresd.Api`) | Exposición de endpoints HTTP de la aplicación |
| **Servicio de autenticación** (`CoppAddresd.Auth`) | Servicio independiente para identidad, login y JWT |
| **Pruebas** (`UnitTests` e `IntegrationTests`) | Suites de tests xUnit |

> ⚠️ **Estado actual:** el proyecto está en fase de **esqueleto inicial** — la arquitectura, los paquetes y la estructura de carpetas están preparados, pero los módulos de negocio aún no se han implementado.

---

## ✨ Características

- ✅ **Clean Architecture** con separación en capas: `Domain → Application → Infrastructure → Api`
- ✅ **CQRS** con **MediatR** para los casos de uso de la capa de aplicación
- ✅ **Validación de entrada** con **FluentValidation**
- ✅ **Entity Framework Core 10** con **PostgreSQL** (Npgsql) como proveedor
- ✅ **ASP.NET Core Identity** para gestión de usuarios
- ✅ **Autenticación JWT Bearer** preparada (paquetes instalados)
- ✅ **OpenAPI** para documentación automática de la API
- ✅ **xUnit** con proyectos de pruebas unitarias e integración
- ✅ Solución en formato `.slnx` (nuevo formato de solución XML)

---

## 🧱 Stack tecnológico

| Capa | Tecnología | Versión |
|---|---|---|
| Runtime | .NET / C# | .NET 10 / C# 13 |
| Framework web | ASP.NET Core (Minimal API) | 10.0 |
| ORM | Entity Framework Core + Npgsql | 10.0.10 / 10.0.3 |
| Base de datos | PostgreSQL | 16+ |
| CQRS / Mediator | MediatR | 14.2.0 |
| Validación | FluentValidation | 12.1.1 |
| Autenticación | ASP.NET Core Identity + JWT Bearer | 10.0.10 |
| Documentación | Microsoft.AspNetCore.OpenApi | 10.0.5 |
| Testing | xUnit + Microsoft.NET.Test.Sdk | 2.9.3 / 17.14.1 |
| Cobertura | coverlet.collector | 6.0.4 |

---

## 🏛️ Arquitectura

El proyecto sigue **Clean Architecture** con una dependencia estrictamente unidireccional: las capas internas no conocen nada de las externas. La regla de dependencia apunta **hacia adentro**.

```
┌────────────────────────────────────────────────────┐
│                   Presentation                      │
│         CoppAddresd.Api + CoppAddresd.Auth          │
│        (HTTP, Controllers, Middleware, OpenAPI)     │
└─────────────────────────┬──────────────────────────┘
                          │
┌─────────────────────────▼──────────────────────────┐
│                  Infrastructure                      │
│  (EF Core, PostgreSQL, Identity, JWT, Repos, Email)  │
└─────────────────────────┬──────────────────────────┘
                          │
┌─────────────────────────▼──────────────────────────┐
│                  Application                        │
│      (MediatR, FluentValidation, DTOs, Interfaces)   │
└─────────────────────────┬──────────────────────────┘
                          │
┌─────────────────────────▼──────────────────────────┐
│                     Domain                          │
│        (Entities, ValueObjects, Enums, Exceptions)   │
└────────────────────────────────────────────────────┘
```

### Responsabilidades por capa

| Capa | Responsabilidad | Depende de |
|---|---|---|
| **Domain** | Entidades, objetos de valor, enumerados y excepciones de dominio. Sin dependencias externas. | — |
| **Application** | Casos de uso (CQRS), validación, DTOs y contratos (interfaces). Define *qué* hace el sistema. | Domain |
| **Infrastructure** | Implementaciones concretas: EF Core, PostgreSQL, Identity, JWT, servicios externos. | Domain, Application |
| **Api / Auth** | Presentación HTTP, inyección de dependencias, middleware y configuración. | Application, Infrastructure |

> El servicio `CoppAddresd.Auth` vive en `src/Services/` como un **servicio desacoplado**: gestiona identidad y emisión de tokens de forma independiente de la API principal.

---

## 📂 Estructura del proyecto

```
coppAddresdBack/
├── CoppAddresd.slnx                        # Solución (.NET 10)
├── .gitignore                              # Exclusiones de control de versiones
│
├── src/
│   ├── CoppAddresd.Api/                    # API principal (ASP.NET Core)
│   │   ├── Configuration/                  #   Opciones de configuración
│   │   ├── Controllers/                    #   Controladores HTTP
│   │   ├── Extensions/                     #   Extensiones de servicios
│   │   ├── Middleware/                     #   Middleware personalizado
│   │   └── Program.cs                      #   Punto de entrada
│   │
│   ├── CoppAddresd.Application/            # Casos de uso y lógica de aplicación
│   │   ├── Behaviors/                      #   Pipelines de MediatR
│   │   ├── Common/                         #   Utilidades compartidas
│   │   ├── DTOs/                           #   Objetos de transferencia
│   │   ├── Features/                       #   Módulos por funcionalidad (CQRS)
│   │   ├── Interfaces/                     #   Contratos
│   │   ├── Mappings/                       #   Perfiles de mapeo
│   │   └── Services/                       #   Servicios de aplicación
│   │
│   ├── CoppAddresd.Domain/                 # Entidades y reglas de negocio
│   │   ├── Entities/                       #   Entidades del dominio
│   │   ├── Enums/                          #   Enumerados
│   │   ├── Exceptions/                     #   Excepciones de dominio
│   │   └── ValueObjects/                   #   Objetos de valor
│   │
│   ├── CoppAddresd.Infrastructure/         # Implementaciones de infraestructura
│   │   ├── Configurations/                 #   Configuraciones de EF Core
│   │   ├── Identity/                       #   Integración con ASP.NET Identity
│   │   ├── Persistence/                    #   DbContext y repositorios
│   │   └── Services/                       #   Servicios de infraestructura
│   │
│   └── Services/
│       └── CoppAddresd.Auth/               # Servicio de autenticación
│
└── tests/
    ├── CoppAddresd.UnitTests/              # Pruebas unitarias (xUnit)
    └── CoppAddresd.IntegrationTests/       # Pruebas de integración (xUnit)
```

---

## ✅ Requisitos previos

| Requisito | Versión | Notas |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0+ | Verifica con `dotnet --version` |
| [PostgreSQL](https://www.postgresql.org/download/) | 16+ | Necesario cuando se configure la persistencia |
| IDE | Visual Studio 2022 / Rider / VS Code | Compatible con soluciones `.slnx` |

---

## ⚙️ Configuración

### ⚠️ Importante: archivos `appsettings`

Los archivos `appsettings.json` y `appsettings.*.json` **están excluidos del control de versiones** (`.gitignore`). Debes crearlos localmente antes de ejecutar el proyecto:

```bash
# Desde la raíz del repositorio
cd src/CoppAddresd.Api
cp appsettings.Example.json appsettings.json   # si existe la plantilla
```

> 💡 **Recomendación:** mantener una plantilla `appsettings.Example.json` versionada con la estructura de configuración (sin secretos) para facilitar la incorporación de nuevos desarrolladores.

### Claves de configuración actuales

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Configuración esperada (futura)

Los paquetes de **EF Core + PostgreSQL**, **Identity** y **JWT** ya están instalados. Cuando se implemente la persistencia y la autenticación, la configuración incluirá secciones como:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=coppaddresd;Username=coppai;Password=coppai"
  },
  "Jwt": {
    "Issuer": "CoppAddresd",
    "Audience": "CoppAddresdClient",
    "SecretKey": "TU_SECRETO_MUY_LARGO_Y_SEGURO",
    "ExpirationMinutes": 60
  }
}
```

> 🔐 **Seguridad:** nunca versiones secretos reales. Usa [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) en desarrollo o variables de entorno en producción.

---

## ▶️ Puesta en marcha

### 1. Restaurar dependencias y compilar

```bash
dotnet restore
dotnet build
```

### 2. Ejecutar la API principal

```bash
dotnet run --project src/CoppAddresd.Api
```

### 3. Ejecutar el servicio de autenticación

```bash
dotnet run --project src/Services/CoppAddresd.Auth
```

### Puertos y perfiles

| Servicio | Perfil HTTP | Perfil HTTPS |
|---|---|---|
| **CoppAddresd.Api** | `http://localhost:5122` | `https://localhost:7258` |
| **CoppAddresd.Auth** | `http://localhost:5058` | `https://localhost:7230` |

Los perfiles se definen en `Properties/launchSettings.json` (perfiles `http` y `https`), con la variable `ASPNETCORE_ENVIRONMENT=Development` por defecto.

### Peticiones de ejemplo

Cada proyecto incluye un archivo `.http` listo para usar con el cliente REST del IDE (VS Code / Rider):

- `src/CoppAddresd.Api/CoppAddresd.Api.http`
- `src/Services/CoppAddresd.Auth/CoppAddresd.Auth.http`

---

## 📖 Documentación de la API (OpenAPI)

El proyecto usa el soporte nativo de **OpenAPI** de ASP.NET Core (paquete `Microsoft.AspNetCore.OpenApi`) + **Swagger UI** (Swashbuckle).

- **Swagger UI** (modo Development): `http://localhost:5122/swagger`
- El documento OpenAPI se genera en **modo Development** en: `GET /openapi/v1.json`
- Puedes inspeccionarlo con [Swagger Editor](https://editor.swagger.io/), [Postman](https://www.postman.com/) o cualquier cliente OpenAPI.

---

## 🧪 Testing

```bash
# Ejecutar todas las pruebas
dotnet test

# Pruebas unitarias únicamente
dotnet test tests/CoppAddresd.UnitTests

# Pruebas de integración únicamente
dotnet test tests/CoppAddresd.IntegrationTests

# Filtrar por nombre de prueba
dotnet test --filter "FullyQualifiedName~MiPrueba"
```

Ambos proyectos usan **xUnit** con `Microsoft.NET.Test.Sdk` y `coverlet.collector` (cobertura de código disponible mediante `dotnet test --collect:"XPlat Code Coverage"`).

---

## 🔐 Servicio de autenticación

`CoppAddresd.Auth` es un servicio web independiente dedicado a la autenticación:

- **ASP.NET Core Identity** para registro, login y gestión de usuarios
- **JWT Bearer** para emisión y validación de tokens
- **EF Core + PostgreSQL** como almacenamiento de identidad
- Documentación OpenAPI propia en `GET /openapi/v1.json`

Ejecución:

```bash
dotnet run --project src/Services/CoppAddresd.Auth
```

---

## 📐 Convenciones del proyecto

- **Clean Architecture** estricta: la dependencia fluye siempre hacia el dominio (regla de dependencia hacia adentro).
- **CQRS con MediatR**: los casos de uso viven en `Features/` organizados por funcionalidad (`Command` / `Query`).
- **Validación con FluentValidation** en la capa de aplicación.
- **C# moderno**: `Nullable` habilitado, `ImplicitUsings` habilitado, records y Minimal APIs donde aplique.
- **Solución en formato `.slnx`** (XML) en lugar del clásico `.sln`.
- Comentarios y documentación en **español**.
- Estructura de carpetas pre-creada (con `.gitkeep`) para cada módulo planeado.

---

## 🗺️ Próximos pasos

- [ ] Configurar `DbContext` y migraciones de EF Core (PostgreSQL)
- [ ] Implementar ASP.NET Identity + JWT en el servicio de autenticación
- [ ] Desarrollar los primeros módulos de negocio en `Features/` (CQRS)
- [ ] Crear `appsettings.Example.json` como plantilla versionada
- [ ] Añadir Dockerfile / docker-compose para el backend
- [ ] Configurar CI/CD (GitHub Actions)
- [ ] Migrar el endpoint `/weatherforecast` de plantilla a endpoints reales

---

## 🤝 Contribución

1. Haz un *fork* del repositorio.
2. Crea una rama descriptiva: `git checkout -b feature/mi-funcionalidad`
3. Realiza tus cambios siguiendo las [convenciones del proyecto](#-convenciones-del-proyecto).
4. Asegúrate de que las pruebas pasen: `dotnet test`
5. Envía un *pull request* describiendo los cambios.

---

## 📄 Licencia

Este proyecto está bajo la licencia **MIT**. Consulta el archivo `LICENSE` para más detalles.
