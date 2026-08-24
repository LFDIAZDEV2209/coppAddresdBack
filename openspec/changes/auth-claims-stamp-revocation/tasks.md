# Tasks: auth-claims-stamp-revocation

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 350-500 |
| 400-line budget risk | Medium |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 → PR 2 |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Stamp wiring + claim emission (REQ-STAMP-01/02/03 + REQ-CLAIMS-01/02) | PR 1 | Foundation: DI, OnTokenValidated, claims in tokens. Tests included. |
| 2 | Handler claim switch + UserService gap + verification tests (REQ-CLAIMS-03 + REQ-INVALID-03 + REQ-AUDIT-*) | PR 2 | Depends on PR 1. Handler, UserService, integration tests. |

---

## Phase 1: Test Infrastructure (prerequisite for all phases)

- [x] 1.1 Add `ProjectReference` to `src/Services/CoppAddresd.Auth` in `tests/CoppAddresd.UnitTests/CoppAddresd.UnitTests.csproj`. Add `<PackageReference Include="NSubstitute" />` for mocking.
- [x] 1.2 Verify `dotnet build tests/CoppAddresd.UnitTests` compiles after csproj change.

## Phase 2: Stamp Wiring (REQ-STAMP-01, REQ-STAMP-02, REQ-STAMP-03)

- [x] 2.1 **RED**: Write `SecurityStampValidatorTests.cs` — test `ValidateAsync` returns false when stamp mismatches, returns false when user not found, returns false when user inactive. (3 tests)
- [x] 2.2 **GREEN**: Register `ISecurityStampValidator` → `SecurityStampValidator` in `Program.cs` DI (line ~55).
- [x] 2.3 **RED**: Write `AddAuthJwtStampTests.cs` — test that `OnTokenValidated` is wired by asserting `SecurityStampValidator.ValidateAsync` is called for `aud==erp` tokens and skipped for `aud==app` tokens.
- [x] 2.4 **GREEN**: Add `OnTokenValidated` event in `ServiceCollectionExtensions.AddAuthJwt` — resolve `ISecurityStampValidator`, check `aud == "erp"`, call `ValidateAsync`, reject with 401 on false. Inject `IServiceProvider` via events.
- [x] 2.5 **REFACTOR**: Verify all stamp tests pass. Confirm `app` tokens are unaffected.

## Phase 3: Claim Emission (REQ-CLAIMS-01, REQ-CLAIMS-02)

- [x] 3.1 **RED**: Write `TokenServiceClaimsTests.cs` — test `GenerateAccessToken` emits claim for each permission code (use `GetUserAllPermissionCodesAsync` spy returning `["Users.View","Roles.Assign"]`). Assert `ClaimsPrincipal` contains both. Test empty permission set → zero permission claims. (2 tests)
- [x] 3.2 **GREEN**: Modify `TokenService.GenerateAccessToken` — add `IEnumerable<string> permissions` parameter. Emit each permission code as claim with type `"permission"`. Update `AuthService.LoginAsync` and `AuthService.RefreshAsync` to call `IPermissionService.GetUserAllPermissionCodesAsync` and pass result to `GenerateAccessToken`. (Files: `TokenService.cs`, `AuthService.cs`, `ITokenService.cs`)
- [x] 3.3 **RED**: Write `AuthServiceClaimsIntegrationTests.cs` — test login produces token with current permissions; test refresh recomputes permissions (grant new permission before refresh, assert new token has it). (2 tests)
- [x] 3.4 **GREEN**: Implement claim recomputation in `AuthService.RefreshAsync` — call `GetUserAllPermissionCodesAsync` instead of reusing old claims.

## Phase 4: Handler Claim Switch (REQ-CLAIMS-03)

- [x] 4.1 **RED**: Write `PermissionHandlerClaimsTests.cs` — test handler succeeds when JWT has required claim, fails with 403 when claim missing, and `IPermissionService.UserHasPermissionAsync` is NEVER called (spy assertion). (3 tests)
- [x] 4.2 **GREEN**: Rewrite `PermissionHandler.HandleRequirementAsync` — read `"permission"` claims from `context.User`, check if `requirement.PermissionCode` is present. Remove `_permissionService.UserHasPermissionAsync` call. Keep `_permissionService` injection for future rollback flag. (File: `PermissionHandler.cs`)

## Phase 5: UserService Gap Fix (REQ-INVALID-03)

- [x] 5.1 **RED**: Write `UserServiceInvalidationTests.cs` — test: when `UpdateAsync` sets `IsActive=false`, `ITokenInvalidationService.InvalidateUserTokensAsync` is called; when `IsActive` is not changed, it is NOT called; when re-enabling, stamp is NOT re-bumped. (3 tests)
- [x] 5.2 **GREEN**: Inject `ITokenInvalidationService` into `UserService`. In `UpdateAsync`, snapshot `user.IsActive` before change, after `UpdateAsync` succeeds if `wasActive && !user.IsActive` call `InvalidateUserTokensAsync`. (File: `UserService.cs`, `IUserService.cs` if needed)

## Phase 6: Verification & Coverage Tests (REQ-INVALID-01/02/04, REQ-AUDIT-01/02)

- [x] 6.1 **RED**: Write `InvalidationVerificationTests.cs` — verify `PermissionService.AssignToUserAsync` calls `InvalidateUserTokensAsync` (already implemented, test confirms). Verify `RoleService.AssignToUserAsync` calls it. Verify `AuthService.ChangePasswordAsync` bumps stamp + revokes refresh tokens. (3 tests)
- [x] 6.2 **RED**: Write `AudienceIsolationTests.cs` — test: `app` JWT on `erp`-scoped endpoint returns 401; `erp` JWT on `app`-scoped endpoint returns 401. (2 tests)
- [x] 6.3 Run full test suite: `dotnet test tests/CoppAddresd.UnitTests`. Confirm all tests pass.

## Phase 7: Cleanup

- [x] 7.1 Verify no `IPermissionService.UserHasPermissionAsync` calls remain in the hot path (PermissionHandler). If feature flag retained, document it.
- [x] 7.2 Update `docs/modules/auth.md` (if exists) or add inline doc comment in `PermissionHandler` explaining claim-based auth + stamp validation for ERP.
- [x] 7.3 Confirm `dotnet build src/Services/CoppAddresd.Auth` passes with zero warnings.

## Phase 8: Post-judgment fixes (audience bypass, batched invalidation, refresh race)

- [x] 8.1 **RED**: Rewrite `AudienceIsolationTests` to build from the REAL production config (`Jwt:ValidAudiences = ["erp","app"]`) and add `ErpAudience` policy tests: app token denied, erp token allowed, no-aud denied, end-to-end chain (middleware accepts → policy denies), controller attribute coverage. (REQ-AUDIT-01/03)
- [x] 8.2 **GREEN**: Add `ErpAudienceRequirement` + `ErpAudienceHandler` + `RequireErpAudienceAttribute`; register policy + handler in `Program.cs`; apply `[RequireErpAudience]` at class level on `UsersController`, `RolesController`, `PermissionsController`. App-audience behavior unchanged (no stamp check for "app").
- [x] 8.3 **RED**: Add `TokenInvalidationServiceTests` (SQLite: batch updates all, empty no-op, unknown ids skipped, ct cancellation on both paths) and update `InvalidationVerificationTests` to the batched contract (one `InvalidateUsersTokensAsync` call instead of the per-user loop) + ct propagation tests for PermissionService/RoleService. (REQ-INVALID-05)
- [x] 8.4 **GREEN**: Add `ITokenInvalidationService.InvalidateUsersTokensAsync`; implement in `TokenInvalidationService` with a single `ExecuteUpdateAsync ... WHERE Id IN (...)` (AuthDbContext injected); PermissionService role paths use the batch with ct; PermissionService user paths and RoleService pass ct.
- [x] 8.5 **RED**: Add `RefreshRotationConcurrencyTests` (SQLite shared memory DB, two contexts): concurrent same-token refresh → exactly one success + one mint; sequential replay → rejected. Switch `AuthServiceClaimsIntegrationTests` fixture to SQLite (ExecuteUpdateAsync unsupported by InMemory). (REQ-REFRESH-01)
- [x] 8.6 **GREEN**: `AuthService.RefreshAsync` claims the token atomically (`ExecuteUpdateAsync WHERE token = @t AND revoked_at IS NULL`); affected == 0 → return null (no new family). Tracker mirrored to avoid SaveChanges reverting the claim.
- [x] 8.7 Update `docs/modules/auth/README.md` (ERP-only endpoints, atomic rotation, batched invalidation) and openspec requirements/scenarios. Confirm `dotnet build` (0 warnings) + full unit suite green.
