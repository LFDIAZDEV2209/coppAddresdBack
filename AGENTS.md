# CoppAddresd Backend — Agent Guide

.NET 10 / C# 13 backend, Clean Architecture, **skeleton state**: only template boilerplate exists. Business modules (Identity, JWT, EF/PostgreSQL, MediatR) are package-installed but NOT wired up.

## Commands

```bash
dotnet build                                  # build solution (uses CoppAddresd.slnx)
dotnet test                                   # all tests (xUnit)
dotnet test tests/CoppAddresd.UnitTests       # unit tests only
dotnet test --filter "FullyQualifiedName~X"   # single test
dotnet run --project src/CoppAddresd.Api      # main API (http://localhost:5122, https 7258)
dotnet run --project src/Services/CoppAddresd.Auth  # auth service (http://localhost:5058, https 7230)
dotnet --version                              # needs 10.0+
```

No lint/format script. Build passes before considering work done.

## Project layout & architecture

Dependency flow inward, enforced only by csproj references (currently correct — don't add outward refs):

- `src/CoppAddresd.Domain` — entities/ValueObjects/enums/exceptions. No deps.
- `src/CoppAddresd.Application` — MediatR handlers in `Features/`, FluentValidation, DTOs, interfaces. Deps: Domain.
- `src/CoppAddresd.Infrastructure` — EF Core, PostgreSQL (Npgsql), Identity, JWT. Deps: Domain, Application.
- `src/CoppAddresd.Api` — minimal API host. Deps: Application, Infrastructure.
- `src/Services/CoppAddresd.Auth` — **standalone** web service, references NO other project (only NuGet: JwtBearer, Identity EF, Npgsql). Intended home of Identity + JWT implementation.

`CoppAddresd.slnx` is the new XML solution format — plain `.sln` does not exist. Tools expecting `.sln` will fail.

## Gotchas

- **`appsettings.json` / `appsettings.*.json` are gitignored** (`src/CoppAddresd.Api` and `src/Services/CoppAddresd.Auth`). They must be created locally before running. There is NO committed `appsettings.Example.json` template yet — README's config keys are aspirational, nothing is actually read by code.
- Both `Program.cs` files are unchanged ASP.NET template code: only `/weatherforecast` endpoint + OpenAPI. No middleware, no DI registrations, no DbContext.
- `src/CoppAddresd.Domain`, `.Application`, `.Infrastructure` contain only placeholder `Class1.cs`; folder structure (Behaviors/, Features/, Persistence/, etc.) is pre-created with `.gitkeep` — use it.
- Tests (`UnitTest1.cs` in both test projects) are template stubs and reference NO src projects — real test projects need project references added.
- Comments/docs in **Spanish** per README convention.

## Project skills & docs (MANDATORY before substantial work)

- **Project standards skills** live in `.agents/skills/` (architecture, entity-framework, linq, pagination, query-performance, database-indexes, database-normalization, database, concurrency, cancellation-token, transactions, n-plus-one, production-performance, aws-production, api-design, repository-pattern, service-layer, error-handling, logging, caching, testing, migrations, documentation, security, dotnet). Load `architecture` FIRST, then the ones matching the module — they encode production/PostgreSQL/concurrency rules (N+1, CancellationToken propagation, keyset pagination, index documentation, transaction hygiene, secrets policy).
- `docs/` — per-module + database/performance/AWS docs in Spanish; update the module doc in the same task that changes code. Docs and skills are Spanish (repo convention).
- Missing standard skills (csharp-*, dotnet-*, aspnet-core) also exist in `.agents/skills/`.

## Sources of truth

- `README.md` — full architecture, stack versions (MediatR 14.2.0, FluentValidation 12.1.1, EF 10.0.10/Npgsql 10.0.3), ports. Mostly accurate for the skeleton state.

<!-- CODEGRAPH_START -->
## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
