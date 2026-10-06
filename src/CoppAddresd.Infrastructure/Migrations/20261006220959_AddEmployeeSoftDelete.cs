using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_employees_organization_email",
                schema: "erp",
                table: "employees");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                schema: "erp",
                table: "employees",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_employees_deleted_at",
                schema: "erp",
                table: "employees",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "ix_employees_organization_email",
                schema: "erp",
                table: "employees",
                columns: new[] { "organization_id", "email" },
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_employees_deleted_at",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "ix_employees_organization_email",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "erp",
                table: "employees");

            migrationBuilder.CreateIndex(
                name: "ix_employees_organization_email",
                schema: "erp",
                table: "employees",
                columns: new[] { "organization_id", "email" },
                unique: true);
        }
    }
}
