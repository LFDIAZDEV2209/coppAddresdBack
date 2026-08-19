using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Auth.Migrations
{
    /// <inheritdoc />
    public partial class AddScopedAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScopedPermissionAssignments",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Effect = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    GrantedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScopedPermissionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScopedPermissionAssignments_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "auth",
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScopedPermissionAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScopedRoleAssignments",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrantedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScopedRoleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScopedRoleAssignments_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScopedRoleAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScopedPermissionAssignments_PermissionId",
                schema: "auth",
                table: "ScopedPermissionAssignments",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScopedPermissionAssignments_UserId",
                schema: "auth",
                table: "ScopedPermissionAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScopedRoleAssignments_RoleId",
                schema: "auth",
                table: "ScopedRoleAssignments",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScopedRoleAssignments_UserId",
                schema: "auth",
                table: "ScopedRoleAssignments",
                column: "UserId");

            // Unicidad sin duplicados: NULLS NOT DISTINCT permite una sola fila
            // por (user, role/permission, scopeType, scopeId) incluso cuando
            // scopeId es NULL (scope Global). PG 15+.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ix_scoped_role_assignments_unique
                    ON auth."ScopedRoleAssignments" ("UserId", "RoleId", "ScopeType", "ScopeId") NULLS NOT DISTINCT;

                CREATE UNIQUE INDEX ix_scoped_permission_assignments_unique
                    ON auth."ScopedPermissionAssignments" ("UserId", "PermissionId", "ScopeType", "ScopeId") NULLS NOT DISTINCT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScopedPermissionAssignments",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "ScopedRoleAssignments",
                schema: "auth");
        }
    }
}
