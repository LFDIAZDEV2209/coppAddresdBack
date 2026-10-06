using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.UnitTests.Features.Professionals;

/// <summary>
/// Unión pura de asignaciones scoped: el wizard de alta solo agrega roles y
/// permisos; nunca remueve los existentes y jamás pierde el scope de cada uno.
/// </summary>
public class ScopedAssignmentsMergeTests
{
    private static readonly Guid RoleA = Guid.NewGuid();
    private static readonly Guid RoleB = Guid.NewGuid();
    private static readonly Guid PermX = Guid.NewGuid();
    private static readonly Guid PermY = Guid.NewGuid();
    private static readonly Guid ClinicA = Guid.NewGuid();
    private static readonly Guid ClinicB = Guid.NewGuid();

    [Fact]
    public void Union_UsuarioSinAsignaciones_DevuelveLasDelWizard()
    {
        var roles = new[] { new ScopedRoleAssignmentInput(RoleA, "Clinic", ClinicA) };
        var permissions = new[]
        {
            new ScopedPermissionAssignmentInput(PermX, "Clinic", ClinicA, "Grant"),
        };

        var (mergedRoles, mergedPermissions) = ScopedAssignmentsMerge.Union(
            null,
            roles,
            permissions
        );

        Assert.Equal(roles, mergedRoles);
        Assert.Equal(permissions, mergedPermissions);
    }

    [Fact]
    public void Union_PreservaExistentesYSumaNuevas()
    {
        var current = new ScopedAssignmentsResult(
            [new ScopedRoleAssignmentView(RoleA, "Professional", "Clinic", ClinicA)],
            [new ScopedPermissionAssignmentView(PermX, "Docs.View", "Clinic", ClinicA, "Grant")]
        );

        var (mergedRoles, mergedPermissions) = ScopedAssignmentsMerge.Union(
            current,
            [new ScopedRoleAssignmentInput(RoleB, "Clinic", ClinicB)],
            [new ScopedPermissionAssignmentInput(PermY, "Clinic", ClinicB, "Grant")]
        );

        Assert.Equal(2, mergedRoles.Count);
        Assert.Contains(mergedRoles, r => r.RoleId == RoleA && r.ScopeId == ClinicA);
        Assert.Contains(mergedRoles, r => r.RoleId == RoleB && r.ScopeId == ClinicB);
        Assert.Equal(2, mergedPermissions.Count);
        Assert.Contains(mergedPermissions, p => p.PermissionId == PermX && p.ScopeId == ClinicA);
        Assert.Contains(mergedPermissions, p => p.PermissionId == PermY && p.ScopeId == ClinicB);
    }

    [Fact]
    public void Union_MismoRolEnClinicasDistintas_NoSeColapsa()
    {
        var current = new ScopedAssignmentsResult(
            [new ScopedRoleAssignmentView(RoleA, "Professional", "Clinic", ClinicA)],
            []
        );

        var (mergedRoles, _) = ScopedAssignmentsMerge.Union(
            current,
            [new ScopedRoleAssignmentInput(RoleA, "Clinic", ClinicB)],
            []
        );

        Assert.Equal(2, mergedRoles.Count);
        Assert.Contains(mergedRoles, r => r.RoleId == RoleA && r.ScopeId == ClinicA);
        Assert.Contains(mergedRoles, r => r.RoleId == RoleA && r.ScopeId == ClinicB);
    }

    [Fact]
    public void Union_DuplicadoExacto_SeColapsa()
    {
        var current = new ScopedAssignmentsResult(
            [new ScopedRoleAssignmentView(RoleA, "Professional", "Clinic", ClinicA)],
            [new ScopedPermissionAssignmentView(PermX, "Docs.View", "Clinic", ClinicA, "Grant")]
        );

        var (mergedRoles, mergedPermissions) = ScopedAssignmentsMerge.Union(
            current,
            [new ScopedRoleAssignmentInput(RoleA, "Clinic", ClinicA)],
            [new ScopedPermissionAssignmentInput(PermX, "Clinic", ClinicA, "Grant")]
        );

        Assert.Single(mergedRoles);
        Assert.Single(mergedPermissions);
    }

    [Fact]
    public void Union_MismoPermisoConEfectoDistinto_SeConserva()
    {
        var current = new ScopedAssignmentsResult(
            [],
            [new ScopedPermissionAssignmentView(PermX, "Docs.View", "Clinic", ClinicA, "Grant")]
        );

        var (_, mergedPermissions) = ScopedAssignmentsMerge.Union(
            current,
            [],
            [new ScopedPermissionAssignmentInput(PermX, "Clinic", ClinicA, "Deny")]
        );

        Assert.Equal(2, mergedPermissions.Count);
        Assert.Contains(mergedPermissions, p => p.Effect == "Grant");
        Assert.Contains(mergedPermissions, p => p.Effect == "Deny");
    }
}
