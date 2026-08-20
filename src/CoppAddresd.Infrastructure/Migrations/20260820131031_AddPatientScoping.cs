using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "clinic_id",
                schema: "app",
                table: "patient_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "app",
                table: "patient_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                schema: "app",
                table: "patient_profiles",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                schema: "app",
                table: "patient_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "app",
                table: "patient_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_profiles_clinic_id",
                schema: "app",
                table: "patient_profiles",
                column: "clinic_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_profiles_deleted_at",
                schema: "app",
                table: "patient_profiles",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "ix_patient_profiles_location_id",
                schema: "app",
                table: "patient_profiles",
                column: "location_id");

            migrationBuilder.AddForeignKey(
                name: "FK_patient_profiles_clinics_clinic_id",
                schema: "app",
                table: "patient_profiles",
                column: "clinic_id",
                principalSchema: "erp",
                principalTable: "clinics",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_profiles_locations_location_id",
                schema: "app",
                table: "patient_profiles",
                column: "location_id",
                principalSchema: "erp",
                principalTable: "locations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // FKs hacia auth.users (creadas por SQL, igual que user_id): la
            // tabla Users vive en el schema auth y la gestiona el Auth Service.
            migrationBuilder.Sql("""
                ALTER TABLE app.patient_profiles
                    ADD CONSTRAINT fk_patient_profiles_created_by
                    FOREIGN KEY (created_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;

                ALTER TABLE app.patient_profiles
                    ADD CONSTRAINT fk_patient_profiles_updated_by
                    FOREIGN KEY (updated_by) REFERENCES auth."Users" ("Id")
                    ON DELETE SET NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_patient_profiles_clinics_clinic_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_profiles_locations_location_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropIndex(
                name: "ix_patient_profiles_clinic_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropIndex(
                name: "ix_patient_profiles_deleted_at",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropIndex(
                name: "ix_patient_profiles_location_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.Sql("""
                ALTER TABLE app.patient_profiles DROP CONSTRAINT IF EXISTS fk_patient_profiles_created_by;
                ALTER TABLE app.patient_profiles DROP CONSTRAINT IF EXISTS fk_patient_profiles_updated_by;
                """);

            migrationBuilder.DropColumn(
                name: "clinic_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "location_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "app",
                table: "patient_profiles");
        }
    }
}
