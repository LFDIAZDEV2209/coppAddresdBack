# Agent Prompt — Implement Dashboards #5 (Community) and #6 (Inventory)
## CQRS Pre-aggregation for Coppaddresd ERP Backend

---

## Your Mission

Implement CQRS pre-aggregation (Channel Pattern, Fase 1) for the two remaining ERP analytics dashboards in the `coppAddresdBack` project. Everything you need is in the plan files. Read them completely before writing a single line of code.

---

## Repository Location

```
C:\Users\Mauricio Polo\Documents\Coppaddresd\coppAddresdBack\
```

---

## Plan Files (READ THESE FIRST — they are self-contained)

| Dashboard | Plan File |
|---|---|
| #5 — Comunidad ADRED | `docs/modules/community/plan-analytics-dashboard-5.md` |
| #6 — Inventario & Farmacia | `docs/modules/inventory/plan-analytics-dashboard-6.md` |

Each plan file contains:
- Full architecture explanation
- Exact file paths for every new file to create
- Complete C# code for every class/interface
- Exact SQL for atomic upserts
- Injection points in existing handlers
- DI registration instructions
- EF Core migration commands
- Tests to write
- Documentation to create/update

---

## Critical Rules — DO NOT SKIP

### 1. Build & Test commands (Windows — CRITICAL)
```powershell
# Always build the test project first (prevent file locking by Auth service):
dotnet build tests/CoppAddresd.UnitTests/CoppAddresd.UnitTests.csproj --no-dependencies

# Run tests without rebuild:
dotnet test tests/CoppAddresd.UnitTests/CoppAddresd.UnitTests.csproj --no-build

# Build Application layer:
dotnet build src/CoppAddresd.Application/CoppAddresd.Application.csproj --no-dependencies

# Build Infrastructure layer:
dotnet build src/CoppAddresd.Infrastructure/CoppAddresd.Infrastructure.csproj --no-dependencies

# Build Community microservice (separate project):
dotnet build src/Services/CoppAddresd.Community/CoppAddresd.Community.csproj --no-dependencies
```

### 2. Dashboard #5 is a MICROSERVICE — different project
The Community module is **NOT** part of `CoppAddresd.Application` or `CoppAddresd.Infrastructure`. It lives in:
```
src/Services/CoppAddresd.Community/
```
- It uses **HotChocolate GraphQL** (not MediatR). Handlers are methods in `CommunityMutation.cs`.
- It has its own `CommunityDbContext` with schema `community.`.
- Its EF migrations are in `src/Services/CoppAddresd.Community/Migrations/`.
- DI registration is in its own `Program.cs`.
- All new files for Dashboard #5 go inside `src/Services/CoppAddresd.Community/`.

### 3. Dashboard #6 uses the main application stack
The Inventory module IS part of `CoppAddresd.Application` + `CoppAddresd.Infrastructure`. It uses MediatR. New files go in:
- Events interface: `src/CoppAddresd.Application/Features/Inventory/Events/`
- Queue interface: `src/CoppAddresd.Application/Interfaces/`
- Queue implementation + HostedService: `src/CoppAddresd.Infrastructure/Metrics/`
- Domain entity: `src/CoppAddresd.Domain/Entities/`
- EF configuration: `src/CoppAddresd.Infrastructure/Configurations/`

### 4. Optional dependency injection pattern (MANDATORY for Dashboard #6)
All new `IInventoryMetricsQueue` injections in existing handlers MUST use `= null` default:
```csharp
public sealed class CreateEntryCommandHandler(
    IInventoryRepository repository,
    IInventoryMetricsQueue? metricsQueue = null  // ← null default keeps existing tests compiling
) : IRequestHandler<CreateEntryCommand, InventoryEntryDto>
```
Usage: always check `if (metricsQueue != null)` before calling. NEVER use `metricsQueue?.EnqueueAsync(...)`.

### 5. Schema verification for Dashboard #6
Before generating the migration, verify the actual DB schema by running:
```powershell
# Search for ToTable calls in EF configurations for Inventory entities
Get-ChildItem -Recurse -Filter "*.cs" src/CoppAddresd.Infrastructure/Configurations | Select-String "ToTable"
```
If Products/Entries/Exits are in schema `"app"` (not `"erp"`), update `InventoryDailyMetricConfiguration.cs` and all SQL strings in `InventoryMetricsProcessorHostedService.cs` accordingly.

### 6. EF Migration commands
Dashboard #5 (Community microservice):
```powershell
dotnet ef migrations add AddCommunityDailyMetrics `
    --project src/Services/CoppAddresd.Community `
    --startup-project src/Services/CoppAddresd.Community `
    --context CommunityDbContext
```

Dashboard #6 (Main application):
```powershell
dotnet ef migrations add AddInventoryDailyMetrics `
    --project src/CoppAddresd.Infrastructure `
    --startup-project src/CoppAddresd.Api `
    --context AppDbContext
```

### 7. Write rule — Atomic upsert SQL
Every SQL upsert uses `ON CONFLICT DO UPDATE SET total_count = table.total_count + EXCLUDED.total_count` — never replace, always accumulate. For decrements, use `GREATEST(0, total_count - 1)` to avoid negative counts.

### 8. Enqueue ALWAYS happens AFTER SaveChangesAsync
The pattern is:
```csharp
await db.SaveChangesAsync(ct);  // ← write to DB first
// then fire-and-forget metric event:
if (metricsQueue != null)
    await metricsQueue.EnqueueAsync(new XxxMetricEvent(...), ct);
```

---

## Execution Order

1. **Verify** schema by checking EF configurations.
2. **Implement Dashboard #5** (Community) — read the plan, create all files, register DI, generate migration, build, run tests.
3. **Implement Dashboard #6** (Inventory) — read the plan, create all files, modify existing handlers, optimize `GetAnalyticsAsync`, register DI, generate migration, build, run tests.
4. **Create documentation** files referenced at the end of each plan.
5. **Commit** changes following conventional commits (no AI attribution):
   ```
   feat(analytics): add community-adred cqrs preaggregation dashboard #5
   feat(analytics): add inventory-pharmacy cqrs preaggregation dashboard #6
   ```

---

## Acceptance Criteria

- [ ] `dotnet build` passes with 0 errors for all affected projects.
- [ ] All existing tests still pass (646+ passing, 0 failing).
- [ ] New unit tests for Community metrics (at least 5 scenarios).
- [ ] New unit tests for Inventory metrics (at least 8 scenarios).
- [ ] `GetAnalyticsAsync` in `InventoryRepository` reads from rollup when data exists.
- [ ] EF migrations generated without errors.
- [ ] DI registered correctly (Singleton queue + HostedService).
- [ ] `docs/modules/community/analytics.md` and `docs/modules/inventory/analytics.md` created.
- [ ] `docs/architecture/README.md` updated with ADR entries for dashboards #5 and #6.
