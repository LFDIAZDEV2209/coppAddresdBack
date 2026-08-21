using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientProfessionalAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patient_professionals",
                schema: "app",
                columns: table => new
                {
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    relationship_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Assigned"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_professionals", x => new { x.patient_id, x.professional_id });
                    table.ForeignKey(
                        name: "FK_patient_professionals_clinics_clinic_id",
                        column: x => x.clinic_id,
                        principalSchema: "erp",
                        principalTable: "clinics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_patient_professionals_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_patient_professionals_professionals_professional_id",
                        column: x => x.professional_id,
                        principalSchema: "erp",
                        principalTable: "professionals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_patient_professionals_clinic_id",
                schema: "app",
                table: "patient_professionals",
                column: "clinic_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_professionals_professional_status",
                schema: "app",
                table: "patient_professionals",
                columns: new[] { "professional_id", "status" });

            // FK hacia auth.users (creada por SQL, igual que created_by del
            // paciente): la tabla Users vive en el schema auth y la gestiona
            // el Auth Service.
            migrationBuilder.Sql("""
                ALTER TABLE app.patient_professionals
                    ADD CONSTRAINT fk_patient_professionals_created_by
                    FOREIGN KEY (created_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;
                """);

            // Permisos mínimos para el rol de la aplicación (convención app_user).
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.patient_professionals TO app_user;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE app.patient_professionals DROP CONSTRAINT IF EXISTS fk_patient_professionals_created_by;
                """);

            migrationBuilder.DropTable(
                name: "patient_professionals",
                schema: "app");
        }
    }
}
