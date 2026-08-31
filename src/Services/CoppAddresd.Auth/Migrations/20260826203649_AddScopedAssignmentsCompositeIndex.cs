using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Auth.Migrations
{
    /// <inheritdoc />
    public partial class AddScopedAssignmentsCompositeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_scoped_permission_assignments_scope_lookup",
                schema: "auth",
                table: "ScopedPermissionAssignments",
                columns: new[] { "UserId", "ScopeType", "ScopeId", "PermissionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_scoped_permission_assignments_scope_lookup",
                schema: "auth",
                table: "ScopedPermissionAssignments");
        }
    }
}
