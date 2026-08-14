using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "app",
                table: "patient_profiles",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "address",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "alcohol_status",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "blood_type",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "disability",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_number",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_type",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "emergency_contact",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ethnicity",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exercise_level",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_name",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "hospitalization_history",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "insurer_id",
                schema: "app",
                table: "patient_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "medical_record_number",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "member_id",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "middle_name",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                schema: "app",
                table: "patient_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "postal_code",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "smoking_status",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "state",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Activo");

            migrationBuilder.AddColumn<string>(
                name: "surgery_history",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "insurers",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_insurers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "patient_allergies",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allergen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_allergies", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_allergies_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient_diagnoses",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    icd10_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_diagnoses", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_diagnoses_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patient_medications",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ndc = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    rx_norm = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    drug_class = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    frequency = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_medications", x => x.id);
                    table.ForeignKey(
                        name: "FK_patient_medications_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vital_signs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    measured_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    systolic = table.Column<int>(type: "integer", nullable: true),
                    diastolic = table.Column<int>(type: "integer", nullable: true),
                    heart_rate = table.Column<int>(type: "integer", nullable: true),
                    temperature_c = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    o2_saturation = table.Column<int>(type: "integer", nullable: true),
                    height_cm = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    weight_kg = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vital_signs", x => x.id);
                    table.ForeignKey(
                        name: "FK_vital_signs_patient_profiles_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "app",
                        principalTable: "patient_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_patient_profiles_insurer_id",
                schema: "app",
                table: "patient_profiles",
                column: "insurer_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_profiles_medical_record_number",
                schema: "app",
                table: "patient_profiles",
                column: "medical_record_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_profiles_status",
                schema: "app",
                table: "patient_profiles",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_insurers_name",
                schema: "app",
                table: "insurers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_allergies_patient_allergen",
                schema: "app",
                table: "patient_allergies",
                columns: new[] { "patient_id", "allergen" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_diagnoses_patient_id",
                schema: "app",
                table: "patient_diagnoses",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_medications_patient_id",
                schema: "app",
                table: "patient_medications",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_vital_signs_patient_measured_at",
                schema: "app",
                table: "vital_signs",
                columns: new[] { "patient_id", "measured_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_patient_profiles_insurers_insurer_id",
                schema: "app",
                table: "patient_profiles",
                column: "insurer_id",
                principalSchema: "app",
                principalTable: "insurers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // ------------------------------------------------------------------
            // Datos de referencia: catálogo de aseguradoras (fuente: hoja
            // Patient_Database_50k del Excel de prueba). No se adjuntan triggers
            // de auditoría a las tablas de pacientes: patient_profiles nunca los
            // tuvo (a diferencia de media_items) y la carga masiva de prueba
            // generaría cientos de miles de filas de auditoría.
            // ------------------------------------------------------------------
            migrationBuilder.InsertData(
                schema: "app",
                table: "insurers",
                columns: ["id", "name", "created_at"],
                values: new object[,]
                {
                    { "00000000-0000-0000-0000-000000000001", "UnitedHealth Group", DateTime.UtcNow },
                    { "00000000-0000-0000-0000-000000000002", "WellCare", DateTime.UtcNow },
                    { "00000000-0000-0000-0000-000000000003", "Blue Cross Blue Shield", DateTime.UtcNow },
                    { "00000000-0000-0000-0000-000000000004", "Aetna", DateTime.UtcNow },
                    { "00000000-0000-0000-0000-000000000005", "Anthem Blue Cross", DateTime.UtcNow },
                    { "00000000-0000-0000-0000-000000000006", "Molina Healthcare", DateTime.UtcNow },
                    { "00000000-0000-0000-0000-000000000007", "Medicare", DateTime.UtcNow },
                    { "00000000-0000-0000-0000-000000000008", "Humana", DateTime.UtcNow },
                    { "00000000-0000-0000-0000-000000000009", "Centene Corporation", DateTime.UtcNow },
                    { "00000000-0000-0000-0000-00000000000a", "No Insurance", DateTime.UtcNow },
                    { "00000000-0000-0000-0000-00000000000b", "Medicaid", DateTime.UtcNow },
                    { "00000000-0000-0000-0000-00000000000c", "Cigna", DateTime.UtcNow },
                    { "00000000-0000-0000-0000-00000000000d", "Kaiser Permanente", DateTime.UtcNow },
                    { "00000000-0000-0000-0000-00000000000e", "Tricare", DateTime.UtcNow },
                });

            // Permisos mínimos para el rol de la aplicación (convención app_user).
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.insurers TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.patient_diagnoses TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.patient_medications TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.patient_allergies TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.vital_signs TO app_user;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_patient_profiles_insurers_insurer_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropTable(
                name: "insurers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "patient_allergies",
                schema: "app");

            migrationBuilder.DropTable(
                name: "patient_diagnoses",
                schema: "app");

            migrationBuilder.DropTable(
                name: "patient_medications",
                schema: "app");

            migrationBuilder.DropTable(
                name: "vital_signs",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_patient_profiles_insurer_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropIndex(
                name: "ix_patient_profiles_medical_record_number",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropIndex(
                name: "ix_patient_profiles_status",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "address",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "alcohol_status",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "blood_type",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "city",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "disability",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "document_number",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "document_type",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "emergency_contact",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "ethnicity",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "exercise_level",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "first_name",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "hospitalization_history",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "insurer_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "last_name",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "medical_record_number",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "member_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "middle_name",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "notes",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "postal_code",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "smoking_status",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "state",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "surgery_history",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "app",
                table: "patient_profiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
