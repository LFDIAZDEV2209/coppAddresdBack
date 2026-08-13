using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppAndErpSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "erp");

            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "employees",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    hire_date = table.Column<DateTime>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "patient_profiles",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date_of_birth = table.Column<DateTime>(type: "date", nullable: true),
                    gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_profiles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employees_user_id",
                schema: "erp",
                table: "employees",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_profiles_user_id",
                schema: "app",
                table: "patient_profiles",
                column: "user_id",
                unique: true);

            // ------------------------------------------------------------------
            // FKs hacia auth.users: la tabla Users vive en el schema auth y la
            // gestiona el Auth Service (otro DbContext). La restricción se crea
            // aquí por SQL. Requiere que las migraciones del Auth Service ya se
            // hayan aplicado (el Auth Service las aplica automáticamente al
            // iniciar).
            // ------------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE app.patient_profiles
                    ADD CONSTRAINT fk_patient_profiles_user_id
                    FOREIGN KEY (user_id) REFERENCES auth."Users" ("Id")
                    ON DELETE CASCADE;

                ALTER TABLE erp.employees
                    ADD CONSTRAINT fk_employees_user_id
                    FOREIGN KEY (user_id) REFERENCES auth."Users" ("Id")
                    ON DELETE CASCADE;
                """);

            // Permisos mínimos para el rol de la aplicación (convención app_user).
            migrationBuilder.Sql("""
                GRANT USAGE ON SCHEMA app TO app_user;
                GRANT USAGE ON SCHEMA erp TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.patient_profiles TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.employees TO app_user;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employees",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "patient_profiles",
                schema: "app");
        }
    }
}
