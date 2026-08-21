# auth-claims-stamp-revocation — Requirements

**Domain**: `auth-tokens`
**Scope**: `src/Services/CoppAddresd.Auth` and any API consumer that validates its JWTs.
**Mode**: Delta on top of existing `IPermissionService`, `TokenService`, `SecurityStampValidator`, `PermissionHandler`.

## Purpose

Replace per-request DB permission lookups in `PermissionHandler` with claim-based authorization, while preserving **immediate revocation** for the small, high-churn ERP audience via a security-stamp gate. Patient traffic (app, ~1M concurrent / ~10M registered) MUST stay DB-free on the authorization hot path.

## Audience codes (discriminator = JWT `aud` claim)

| Code | Population | Permission churn | Revocation target |
|---|---|---|---|
| `erp` | Staff/doctors (small) | Frequent | Immediate (next request) |
| `app` | Patients (~10M) | Rare | Up to 15 min via token expiry |

## Requirements

| ID | Requirement |
|---|---|
| **REQ-CLAIMS-01** | The access token issued by `AuthService.LoginAsync` MUST contain, as JWT claims, every permission code the user holds at issuance (direct + via roles), obtained via `IPermissionService.GetUserAllPermissionCodesAsync`. |
| **REQ-CLAIMS-02** | The access token issued by `AuthService.RefreshAsync` MUST contain the **current** full permission set recomputed at refresh time. Refresh MUST NOT reuse or copy claims from the previous token. |
| **REQ-CLAIMS-03** | `PermissionHandler.HandleRequirementAsync` MUST authorize by reading permission codes from `context.User` claims. The DB call to `IPermissionService.UserHasPermissionAsync` MUST be removed from the hot path. (May be retained behind a feature flag for rollback.) |
| **REQ-STAMP-01** | `ISecurityStampValidator` MUST be registered in DI. Currently dead code (no registration in `Program.cs`, no `OnTokenValidated` event in `AddAuthJwt`). |
| **REQ-STAMP-02** | `JwtBearerEvents.OnTokenValidated` MUST invoke `ISecurityStampValidator.ValidateAsync` **only when `aud == "erp"`**. Tokens with `aud == "app"` MUST skip the stamp check entirely. The `aud` claim is the discriminator. |
| **REQ-STAMP-03** | When `ValidateAsync` returns `false`, the request MUST be rejected as 401 Unauthorized by the JWT middleware. The principal MUST NOT proceed to authorization. |
| **REQ-INVALID-01** | `PermissionService.AssignToRoleAsync`, `RemoveFromRoleAsync`, `AssignToUserAsync`, `RemoveFromUserAsync` MUST invalidate tokens for every affected user (single-user paths via `InvalidateUserTokensAsync`; role-scoped paths via batched `InvalidateUsersTokensAsync`). |
| **REQ-INVALID-02** | `RoleService.AssignToUserAsync` and `RemoveFromUserAsync` MUST call `ITokenInvalidationService.InvalidateUserTokensAsync`. |
| **REQ-INVALID-03** | `UserService.UpdateAsync` MUST bump the user's security stamp when `IsActive` transitions to `false`. **Gap today**: `UserService` does not inject or call `ITokenInvalidationService`. The handler MUST inject it and call it on every `IsActive → false` flip (re-enable does not clear the bump). |
| **REQ-INVALID-04** | `AuthService.ChangePasswordAsync` MUST bump the security stamp after a successful password change and revoke active refresh tokens. (Already implemented; verify with tests.) |
| **REQ-INVALID-05** | `ITokenInvalidationService.InvalidateUsersTokensAsync(IEnumerable<Guid>, CancellationToken)` MUST update the security stamp of ALL affected users in a SINGLE statement (`ExecuteUpdateAsync ... WHERE Id IN (...)`, one round trip, no per-user loop), and ALL invalidation call sites MUST propagate the request `CancellationToken`. Partial failures are sane: unknown user ids are skipped without throwing (count logged). |
| **REQ-MIGRATE-01** | The deployment MUST follow this sequence — never skip or reorder: **(a)** wire `ISecurityStampValidator` DI + `OnTokenValidated` for `aud == erp` (no behavior change for `app`); **(b)** emit permission claims in tokens while `PermissionHandler` still queries DB (double-validation, safe); **(c)** switch `PermissionHandler` to claim-only. Shipping step (c) without (a)+(b) is a security regression for ERP users. |
| **REQ-AUDIT-01** | Cross-audience replay MUST be rejected on ERP admin endpoints: an `app` token presented on a `[RequireErpAudience]` endpoint (Users/Roles/Permissions) is denied. **Mechanism (updated by fix)**: the JWT middleware accepts BOTH known audiences with the real production config (`Jwt:ValidAudiences = ["erp","app"]`), so isolation is enforced in the authorization layer — the `ErpAudience` policy requires `aud == "erp"` and denies an `app` token with 403 (previously the hypothetical single-audience middleware config rejected it with 401, which the real config never did). The reverse direction (erp token on app-only endpoints) does not apply: there are no app-only `[RequirePermission]` endpoints. |
| **REQ-AUDIT-02** | Disabled users MUST be denied at the next login AND next refresh attempt. Live access tokens for `app` users MAY remain valid until natural expiry (≤ 15 min) because the stamp check does not run for `app` — this is an explicit, accepted trade-off. ERP users get immediate denial via stamp. |
| **REQ-AUDIT-03** | The ERP admin endpoints (`UsersController`, `RolesController`, `PermissionsController`) MUST be reachable ONLY with `aud == "erp"` tokens, enforced per-endpoint via `[RequireErpAudience]` (policy `ErpAudience`, handler checks the `aud` claim). Rationale: a staff user holding a `UserApplication` for "app" receives their full ERP permission set in an app-audience token; since `app` skips the stamp check, without this policy revocation could be bypassed for up to 15 min through the app channel. `[AllowAnonymous]` actions (e.g. `POST /api/users`) remain unrestricted. |
| **REQ-REFRESH-01** | `AuthService.RefreshAsync` MUST rotate the refresh token ATOMICALLY: a conditional `UPDATE ... SET revoked_at = now() WHERE token = @t AND revoked_at IS NULL` claims the token; if it affects 0 rows the token was already used and the request MUST be rejected (`null`) WITHOUT minting a new family. Two concurrent requests with the same token MUST produce exactly one live session (fixes the check-then-act race where replay minted multiple families). |
| **REQ-NGOAL-01** | Non-goals (out of scope): NO refresh-on-deny pattern; NO Redis/distributed cache layer; NO access token lifetime change (15 min stays); NO front-end changes (verify only that `refreshAccessToken` on 401 still works). |

## Coverage matrix vs. today's code

| Concern | Today | After this change |
|---|---|---|
| Permission check on request | DB query per request | Claim read (zero DB) |
| ERP permission revoked | Up to 15 min | Immediate (next request) |
| APP permission revoked | Up to 15 min | Up to 15 min (accepted) |
| Stamp validator wired | Dead code | Wired for `erp` only |
| User disable invalidates tokens | ❌ bug (no stamp bump) | ✅ fixed |
| App token on ERP admin endpoint | ❌ bypass (up to 15 min) | ✅ denied (403, ErpAudience policy) |
| Role mutation invalidation | ❌ N×2 round trips, no ct | ✅ single batch UPDATE, ct propagated |
| Concurrent refresh replay | ❌ multiple families minted | ✅ atomic claim, one family |