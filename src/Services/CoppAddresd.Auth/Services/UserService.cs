using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Entities;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;

namespace CoppAddresd.Auth.Services;

/// <summary>
/// Proyección de solo lectura para resolver nombres de clínicas u
/// organizaciones a partir de sus ids (raw SQL sobre erp.clinics / erp.organizations).
/// </summary>
internal class ScopeNameRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly AuthDbContext _dbContext;
    private readonly ITokenInvalidationService _tokenInvalidation;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        AuthDbContext dbContext,
        ITokenInvalidationService tokenInvalidation,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
        _tokenInvalidation = tokenInvalidation;
        _logger = logger;
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        var scopedRoles = await GetScopedRolesForUsersAsync(new[] { id }, ct);
        scopedRoles.TryGetValue(id, out var scoped);
        return MapToResponse(user, roles, scoped);
    }

    public async Task<IEnumerable<UserResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await _userManager.Users
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync(ct);

        // Batch: cargar todos los roles scoped de los usuarios de UNA sola vez
        // (evita N+1 con GetRolesAsync ya existente por usuario).
        var userIds = users.Select(u => u.Id).ToArray();
        var scopedRolesMap = await GetScopedRolesForUsersAsync(userIds, ct);

        var result = new List<UserResponse>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            scopedRolesMap.TryGetValue(user.Id, out var scoped);
            result.Add(MapToResponse(user, roles, scoped));
        }

        return result;
    }

    /// <summary>
    /// Carga roles scoped en batch para múltiples usuarios: UNA query sobre
    /// ScopedRoleAssignments + Roles (sin N+1). Devuelve un diccionario
    /// UserId → lista de UserScopedRoleResponse (con role name, scope type
    /// y scope name resuelto best-effort).
    /// Protected virtual para que tests puedan overridear la implementación
    /// y evitar la dependencia de raw SQL sobre erp (no disponible en SQLite).
    /// </summary>
    protected virtual async Task<Dictionary<Guid, List<UserScopedRoleResponse>>> GetScopedRolesForUsersAsync(
        Guid[] userIds,
        CancellationToken ct)
    {
        if (userIds.Length == 0)
            return new Dictionary<Guid, List<UserScopedRoleResponse>>();

        // Query batch: ScopedRoleAssignments unido a Roles para obtener el nombre.
        var assignments = await _dbContext.ScopedRoleAssignments
            .Where(a => userIds.Contains(a.UserId))
            .Select(a => new
            {
                a.UserId,
                RoleName = a.Role.Name!,
                a.ScopeType,
                a.ScopeId,
            })
            .ToListAsync(ct);

        if (assignments.Count == 0)
            return new Dictionary<Guid, List<UserScopedRoleResponse>>();

        // Resolver nombres de scopes (clínicas y organizaciones) en batch.
        var clinicIds = assignments
            .Where(a => a.ScopeType == "Clinic" && a.ScopeId.HasValue)
            .Select(a => a.ScopeId!.Value)
            .Distinct()
            .ToArray();
        var orgIds = assignments
            .Where(a => a.ScopeType == "Organization" && a.ScopeId.HasValue)
            .Select(a => a.ScopeId!.Value)
            .Distinct()
            .ToArray();

        var scopeNames = await ResolveScopeNamesAsync(clinicIds, orgIds, ct);

        // Agrupar por usuario.
        var result = new Dictionary<Guid, List<UserScopedRoleResponse>>();
        foreach (var a in assignments)
        {
            var scopeName = a.ScopeId.HasValue
                ? scopeNames.GetValueOrDefault(a.ScopeId.Value)
                : null;

            var item = new UserScopedRoleResponse(
                RoleName: a.RoleName,
                ScopeType: a.ScopeType,
                ScopeName: scopeName);

            if (!result.TryGetValue(a.UserId, out var list))
            {
                list = new List<UserScopedRoleResponse>();
                result[a.UserId] = list;
            }
            list.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Resuelve nombres de clínicas y organizaciones a partir de sus ids
    /// usando SQL crudo sobre erp.clinics y erp.organizations.
    /// Fail-open: si la tabla no existe o la query falla, devuelve un
    /// diccionario vacío (los roles scoped siguen devolviendo ScopeName=null).
    /// Protected virtual para poder mockear en tests unitarios.
    /// </summary>
    protected virtual async Task<Dictionary<Guid, string>> ResolveScopeNamesAsync(
        Guid[] clinicIds,
        Guid[] orgIds,
        CancellationToken ct)
    {
        var result = new Dictionary<Guid, string>();

        if (clinicIds.Length > 0)
        {
            try
            {
                var clinicNames = await _dbContext.Database
                    .SqlQueryRaw<ScopeNameRow>(
                        """SELECT id AS "Id", name AS "Name" FROM erp.clinics WHERE id = ANY(@ids)""",
                        new NpgsqlParameter("ids", clinicIds))
                    .ToListAsync(ct);

                foreach (var row in clinicNames)
                {
                    result[row.Id] = row.Name;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudieron resolver nombres de clínicas para scopes");
            }
        }

        if (orgIds.Length > 0)
        {
            try
            {
                var orgNames = await _dbContext.Database
                    .SqlQueryRaw<ScopeNameRow>(
                        """SELECT id AS "Id", name AS "Name" FROM erp.organizations WHERE id = ANY(@ids)""",
                        new NpgsqlParameter("ids", orgIds))
                    .ToListAsync(ct);

                foreach (var row in orgNames)
                {
                    result[row.Id] = row.Name;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudieron resolver nombres de organizaciones para scopes");
            }
        }

        return result;
    }

    /// <summary>
    /// Busca clínicas por nombre (case-insensitive) usando SQL crudo sobre
    /// erp.clinics. Devuelve todas las coincidencias (0, 1 o N).
    /// Protected virtual para poder mockear en tests unitarios (SQLite
    /// no tiene la tabla erp.clinics).
    /// </summary>
    protected virtual async Task<List<(Guid Id, string Name)>> FindClinicsByNameAsync(
        string name,
        CancellationToken ct)
    {
        try
        {
            var clinics = await _dbContext.Database
                .SqlQueryRaw<ScopeNameRow>(
                    """SELECT id AS "Id", name AS "Name" FROM erp.clinics WHERE LOWER(name) = LOWER(@name)""",
                    new NpgsqlParameter("name", name))
                .Select(c => new ValueTuple<Guid, string>(c.Id, c.Name))
                .ToListAsync(ct);

            return clinics;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron buscar clínicas por nombre: {ClinicName}", name);
            return [];
        }
    }

    /// <summary>
    /// Busca clínicas por código (case-insensitive) usando SQL crudo sobre
    /// erp.clinics. Devuelve todas las coincidencias (0, 1 o N).
    /// Protected virtual para poder mockear en tests unitarios (SQLite
    /// no tiene la tabla erp.clinics).
    /// Espejo exacto de FindClinicsByNameAsync pero comparando code en
    /// lugar de name.
    /// </summary>
    protected virtual async Task<List<(Guid Id, string Name)>> FindClinicsByCodeAsync(
        string code,
        CancellationToken ct)
    {
        try
        {
            var clinics = await _dbContext.Database
                .SqlQueryRaw<ScopeNameRow>(
                    """SELECT id AS "Id", name AS "Name" FROM erp.clinics WHERE LOWER(code) = LOWER(@code)""",
                    new NpgsqlParameter("code", code))
                .Select(c => new ValueTuple<Guid, string>(c.Id, c.Name))
                .ToListAsync(ct);

            return clinics;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron buscar clínicas por código: {ClinicCode}", code);
            return [];
        }
    }

    /// <summary>
    /// Crea el usuario, su password y (si vienen) sus roles y permisos directos
    /// en UNA transacción: si cualquier rol/permiso no existe o falla una
    /// asignación, se revierte todo y se devuelve un único error.
    /// UserManager usa el mismo AuthDbContext, por lo que la transacción cubre
    /// también sus escrituras. Rollback explícito en cada ruta de error
    /// (el `await using` del encabezado es la red de seguridad final).
    /// </summary>
    public async Task<(bool Success, string? Error, UserResponse? User)> CreateAsync(
        CreateUserRequest request,
        CancellationToken ct = default)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            _logger.LogWarning("Create user failed: email {Email} already exists", request.Email);
            return (false, "Email ya está registrado", null);
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            async Task<(bool Success, string? Error, UserResponse? User)> FailAsync(string error)
            {
                // Rollback explícito con CancellationToken.None: no debe abortarse
                // por la cancelación del request (el await using deshace igual).
                await tx.RollbackAsync(CancellationToken.None);
                return (false, error, null);
            }

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                IsActive = true,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Create user failed: {Errors}", errors);
                return await FailAsync(errors);
            }

            if (request.RoleIds is { Length: > 0 })
            {
                var roleSync = await SyncRolesAsync(user, request.RoleIds, ct);
                if (!roleSync.Success)
                {
                    _logger.LogWarning("Create user {UserId} failed assigning roles: {Error}", user.Id, roleSync.Error);
                    return await FailAsync(roleSync.Error!);
                }
            }

            if (request.PermissionIds is { Length: > 0 })
            {
                var permissionSync = await SyncPermissionsAsync(user.Id, request.PermissionIds, ct);
                if (!permissionSync.Success)
                {
                    _logger.LogWarning("Create user {UserId} failed assigning permissions: {Error}", user.Id, permissionSync.Error);
                    return await FailAsync(permissionSync.Error!);
                }
            }

            await tx.CommitAsync(ct);

            _logger.LogInformation("User {UserId} created successfully", user.Id);

            var roles = await _userManager.GetRolesAsync(user);
            return (true, null, MapToResponse(user, roles));
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Carrera de dos POST concurrentes con el mismo email: el pre-chequeo
            // no es atómico y el unique index de Identity lo resuelve acá.
            await tx.RollbackAsync(CancellationToken.None);
            return (false, "Email ya está registrado", null);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Creación masiva de usuarios: cada fila se procesa de forma independiente
    /// (una falla no detiene las demás), sin transacción global.
    /// Carga el catálogo de roles activos una sola vez antes del loop para
    /// resolver RoleName → RoleId con comparación case-insensitive.
    /// Las contraseñas se generan crypto-random en el servidor (≥12 chars,
    /// garantiza upper + lower + dígito + especial, sin chars ambiguos).
    /// </summary>
    public async Task<BulkCreateUsersResult> CreateBulkAsync(
        BulkCreateUsersRequest request,
        CancellationToken ct = default)
    {
        var results = new List<BulkCreateUserRowResult>();

        // Cargar catálogo de roles activos UNA sola vez (evita N+1).
        var activeRoles = await _roleManager.Roles
            .Where(r => r.IsActive)
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(ct);

        var roleNameToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in activeRoles)
        {
            roleNameToId[role.Name!] = role.Id;
        }

        for (var i = 0; i < request.Rows.Count; i++)
        {
            var line = i + 1;
            var row = request.Rows[i];

            // --- Resolver status ---
            bool isActive;
            if (string.IsNullOrWhiteSpace(row.Status))
            {
                isActive = true;
            }
            else if (string.Equals(row.Status, "activo", StringComparison.OrdinalIgnoreCase))
            {
                isActive = true;
            }
            else if (string.Equals(row.Status, "inactivo", StringComparison.OrdinalIgnoreCase))
            {
                isActive = false;
            }
            else
            {
                results.Add(new BulkCreateUserRowResult
                {
                    Line = line,
                    Success = false,
                    Email = row.Email,
                    Error = $"Status inválido: '{row.Status}'. Valores válidos: activo, inactivo."
                });
                continue;
            }

            // --- Resolver RoleName → RoleId ---
            Guid? resolvedRoleId = null;
            if (!string.IsNullOrWhiteSpace(row.RoleName))
            {
                if (roleNameToId.TryGetValue(row.RoleName, out var roleId))
                {
                    resolvedRoleId = roleId;
                }
                else
                {
                    results.Add(new BulkCreateUserRowResult
                    {
                        Line = line,
                        Success = false,
                        Email = row.Email,
                        Error = $"Rol no encontrado (nombre {row.RoleName})"
                    });
                    continue;
                }
            }

            // --- Resolver ClinicCode/ClinicName → clinicId (asignación scoped) ---
            // Precedencia: ClinicCode se usa si está presente; ClinicName se ignora.
            Guid? clinicId = null;
            var hasClinicCode = !string.IsNullOrWhiteSpace(row.ClinicCode);
            var hasClinicName = !string.IsNullOrWhiteSpace(row.ClinicName);
            var hasClinic = hasClinicCode || hasClinicName;

            if (hasClinic)
            {
                // La clínica requiere un rol.
                if (resolvedRoleId is null)
                {
                    results.Add(new BulkCreateUserRowResult
                    {
                        Line = line,
                        Success = false,
                        Email = row.Email,
                        Error = "La clínica requiere un rol"
                    });
                    continue;
                }

                // Si ClinicCode está presente, tiene precedencia sobre ClinicName.
                if (hasClinicCode)
                {
                    var clinics = await FindClinicsByCodeAsync(row.ClinicCode!.Trim(), ct);
                    if (clinics.Count == 0)
                    {
                        results.Add(new BulkCreateUserRowResult
                        {
                            Line = line,
                            Success = false,
                            Email = row.Email,
                            Error = $"Clínica no encontrada (código {row.ClinicCode})"
                        });
                        continue;
                    }
                    if (clinics.Count > 1)
                    {
                        results.Add(new BulkCreateUserRowResult
                        {
                            Line = line,
                            Success = false,
                            Email = row.Email,
                            Error = $"Clínica ambigua (código {row.ClinicCode})"
                        });
                        continue;
                    }
                    clinicId = clinics[0].Id;
                }
                else
                {
                    // ClinicName (comportamiento existente).
                    var clinics = await FindClinicsByNameAsync(row.ClinicName!.Trim(), ct);
                    if (clinics.Count == 0)
                    {
                        results.Add(new BulkCreateUserRowResult
                        {
                            Line = line,
                            Success = false,
                            Email = row.Email,
                            Error = $"Clínica no encontrada (nombre {row.ClinicName})"
                        });
                        continue;
                    }
                    if (clinics.Count > 1)
                    {
                        results.Add(new BulkCreateUserRowResult
                        {
                            Line = line,
                            Success = false,
                            Email = row.Email,
                            Error = $"Clínica ambigua (nombre {row.ClinicName})"
                        });
                        continue;
                    }
                    clinicId = clinics[0].Id;
                }
            }

            // --- Determinar si el rol se asigna global o scoped ---
            Guid[]? globalRoleIds = null;
            if (resolvedRoleId.HasValue && !hasClinic)
            {
                // Sin clínica: rol global (comportamiento existente).
                globalRoleIds = [resolvedRoleId.Value];
            }

            // --- Generar contraseña temporal ---
            var tempPassword = GenerateSecurePassword();

            // --- Crear usuario (CreateAsync asigna roles globales si los hay) ---
            var createRequest = new CreateUserRequest
            {
                Email = row.Email,
                FirstName = row.FirstName,
                LastName = row.LastName,
                Password = tempPassword,
                RoleIds = globalRoleIds,
                PermissionIds = null
            };

            // Override IsActive: CreateAsync siempre pone IsActive=true,
            // así que post-creación ajustamos si es inactivo.
            var (success, error, user) = await CreateAsync(createRequest, ct);

            if (!success)
            {
                results.Add(new BulkCreateUserRowResult
                {
                    Line = line,
                    Success = false,
                    Email = row.Email,
                    Error = error
                });
                continue;
            }

            // --- Asignación scoped (si hay clínica + rol) ---
            if (hasClinic && resolvedRoleId.HasValue && clinicId.HasValue)
            {
                var userGuid = Guid.Parse(user!.Id);

                // Verificar si ya existe una asignación scoped idéntica.
                var alreadyExists = await _dbContext.ScopedRoleAssignments
                    .AnyAsync(s =>
                        s.UserId == userGuid &&
                        s.RoleId == resolvedRoleId.Value &&
                        s.ScopeType == "Clinic" &&
                        s.ScopeId == clinicId.Value, ct);

                if (!alreadyExists)
                {
                    _dbContext.ScopedRoleAssignments.Add(new ScopedRoleAssignment
                    {
                        Id = Guid.NewGuid(),
                        UserId = userGuid,
                        RoleId = resolvedRoleId.Value,
                        ScopeType = "Clinic",
                        ScopeId = clinicId.Value,
                        CreatedAt = DateTime.UtcNow,
                    });
                    await _dbContext.SaveChangesAsync(ct);
                }
            }

            // Si el status es "inactivo", desactivar después de crear.
            if (!isActive)
            {
                var targetUser = await _userManager.FindByIdAsync(user!.Id);
                if (targetUser is not null)
                {
                    targetUser.IsActive = false;
                    targetUser.UpdatedAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(targetUser);
                    // Reflejar en la respuesta.
                    user = new UserResponse(
                        user.Id,
                        user.Email,
                        user.FirstName,
                        user.LastName,
                        IsActive: false,
                        user.CreatedAt,
                        user.Roles);
                }
            }

            results.Add(new BulkCreateUserRowResult
            {
                Line = line,
                Success = true,
                UserId = user!.Id,
                Email = user.Email,
                TemporaryPassword = tempPassword
            });
        }

        return new BulkCreateUsersResult
        {
            Results = results,
            Created = results.Count(r => r.Success),
            Failed = results.Count(r => !r.Success)
        };
    }

    /// <summary>
    /// Genera una contraseña segura crypto-random (≥12 chars) que garantiza
    /// al menos 1 mayúscula, 1 minúscula, 1 dígito y 1 carácter especial.
    /// Excluye caracteres ambiguos (l/I/1/O/0) para legibilidad.
    /// </summary>
    public static string GenerateSecurePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "@#$%&*!?";
        const string all = upper + lower + digits + special;

        const int minLength = 12;
        var password = new char[minLength];

        // Garantizar al menos uno de cada categoría requerida.
        password[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        password[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        password[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        password[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

        for (var i = 4; i < password.Length; i++)
        {
            password[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        // Fisher-Yates shuffle para que las primeras posiciones no sean predecibles.
        for (var i = password.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }

    /// <summary>
    /// Actualiza el perfil y, si el request lo pide, hace sync TOTAL de roles
    /// y permisos directos (conjunto completo deseado). Todo en una transacción.
    /// La invalidación de tokens (security stamp) por desactivación o por
    /// cambio de asignaciones se ejecuta DENTRO de la transacción para que
    /// stamp y asignaciones se persistan atómicamente.
    /// `NotFound` distingue "Usuario no encontrado" (404) de los errores de
    /// payload, ej. "Rol no encontrado (id X)" (400).
    /// </summary>
    public async Task<(bool Success, string? Error, bool NotFound)> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken ct = default)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            async Task<(bool Success, string? Error, bool NotFound)> FailAsync(string error, bool notFound = false)
            {
                await tx.RollbackAsync(CancellationToken.None);
                return (false, error, notFound);
            }

            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user is null)
            {
                return await FailAsync("Usuario no encontrado", notFound: true);
            }

            // Snapshot antes de mutar: permite detectar la transición IsActive → false
            // (REQ-INVALID-03). Re-activar NO re-bumpea el stamp.
            var wasActive = user.IsActive;
            var assignmentsChanged = false;

            if (request.FirstName is not null)
                user.FirstName = request.FirstName;

            if (request.LastName is not null)
                user.LastName = request.LastName;

            if (request.IsActive.HasValue)
                user.IsActive = request.IsActive.Value;

            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Update user {UserId} failed: {Errors}", id, errors);
                return await FailAsync(errors);
            }

            if (request.RoleIds is not null)
            {
                var roleSync = await SyncRolesAsync(user, request.RoleIds, ct);
                if (!roleSync.Success)
                {
                    _logger.LogWarning("Update user {UserId} failed syncing roles: {Error}", id, roleSync.Error);
                    return await FailAsync(roleSync.Error!);
                }
                assignmentsChanged |= roleSync.Changed;
            }

            if (request.PermissionIds is not null)
            {
                var permissionSync = await SyncPermissionsAsync(user.Id, request.PermissionIds, ct);
                if (!permissionSync.Success)
                {
                    _logger.LogWarning("Update user {UserId} failed syncing permissions: {Error}", id, permissionSync.Error);
                    return await FailAsync(permissionSync.Error!);
                }
                assignmentsChanged |= permissionSync.Changed;
            }

            // Desactivar un usuario o cambiar sus roles/permisos invalida todos
            // sus tokens de inmediato (security stamp). Dentro de la transacción
            // para atomicidad con las asignaciones.
            if ((wasActive && !user.IsActive) || assignmentsChanged)
            {
                await _tokenInvalidation.InvalidateUserTokensAsync(id, ct);
                _logger.LogInformation(
                    "User {UserId} assignments/state changed, all tokens invalidated",
                    id);
            }

            await tx.CommitAsync(ct);

            _logger.LogInformation("User {UserId} updated successfully", id);
            return (true, null, false);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return (false, "Usuario no encontrado");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Delete user {UserId} failed: {Errors}", id, errors);
            return (false, errors);
        }

        _logger.LogInformation("User {UserId} deleted successfully", id);
        return (true, null);
    }

    /// <summary>
    /// Sync TOTAL de roles: valida que todos los RoleIds existan (batch query,
    /// sin N+1) y deja al usuario con exactamente ese conjunto (agrega
    /// faltantes, remueve sobrantes). Reporta si hubo cambios efectivos.
    /// </summary>
    private async Task<(bool Success, string? Error, bool Changed)> SyncRolesAsync(
        ApplicationUser user,
        Guid[] roleIds,
        CancellationToken ct)
    {
        var distinctIds = roleIds.Distinct().ToArray();

        var existingRoles = await _dbContext.Roles
            .Where(r => distinctIds.Contains(r.Id))
            .ToListAsync(ct);

        if (existingRoles.Count != distinctIds.Length)
        {
            var existingSet = existingRoles.Select(r => r.Id).ToHashSet();
            var missingId = distinctIds.First(id => !existingSet.Contains(id));
            return (false, $"Rol no encontrado (id {missingId})", false);
        }

        var currentNames = await _userManager.GetRolesAsync(user);
        var currentSet = currentNames.ToHashSet();
        var requestedNames = existingRoles.Select(r => r.Name!).ToHashSet();
        var changed = false;

        foreach (var roleName in requestedNames.Except(currentSet))
        {
            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, errors, false);
            }
            changed = true;
        }

        foreach (var roleName in currentSet.Except(requestedNames))
        {
            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, errors, false);
            }
            changed = true;
        }

        return (true, null, changed);
    }

    /// <summary>
    /// Sync TOTAL de permisos directos: valida que todos los PermissionIds
    /// existan y deja al usuario con exactamente ese conjunto. Reporta si hubo
    /// cambios efectivos.
    /// </summary>
    private async Task<(bool Success, string? Error, bool Changed)> SyncPermissionsAsync(
        Guid userId,
        Guid[] permissionIds,
        CancellationToken ct)
    {
        var distinctIds = permissionIds.Distinct().ToArray();

        var existingIds = await _dbContext.Permissions
            .Where(p => distinctIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);

        var existingSet = existingIds.ToHashSet();
        var missing = distinctIds.FirstOrDefault(id => !existingSet.Contains(id));
        if (missing != Guid.Empty)
        {
            return (false, $"Permiso no encontrado (id {missing})", false);
        }

        // Entidades tracked: los Add/Remove se persisten con un único
        // SaveChangesAsync dentro de la transacción del llamador.
        var currentPermissions = await _dbContext.UserPermissions
            .Where(up => up.UserId == userId)
            .ToListAsync(ct);

        var currentSet = currentPermissions.Select(up => up.PermissionId).ToHashSet();
        var requestedSet = distinctIds.ToHashSet();
        var changed = false;

        foreach (var permissionId in requestedSet.Except(currentSet))
        {
            _dbContext.UserPermissions.Add(new UserPermission
            {
                UserId = userId,
                PermissionId = permissionId
            });
            changed = true;
        }

        foreach (var userPermission in currentPermissions)
        {
            if (!requestedSet.Contains(userPermission.PermissionId))
            {
                _dbContext.UserPermissions.Remove(userPermission);
                changed = true;
            }
        }

        if (changed)
        {
            await _dbContext.SaveChangesAsync(ct);
        }

        return (true, null, changed);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException { SqlState: "23505" };
    }

    private static UserResponse MapToResponse(
        ApplicationUser user,
        IList<string> roles,
        List<UserScopedRoleResponse>? scopedRoles = null)
    {
        return new UserResponse(
            Id: user.Id.ToString(),
            Email: user.Email!,
            FirstName: user.FirstName,
            LastName: user.LastName,
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt,
            Roles: roles.ToArray(),
            ScopedRoles: scopedRoles?.Count > 0 ? scopedRoles.ToArray() : null);
    }
}
