using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeClinicalCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Catálogos de referencia (3NF): allergens, icd10_codes, medications.
            migrationBuilder.CreateTable(
                name: "allergens",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allergens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "icd10_codes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_icd10_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "medications",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ndc = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    rx_norm = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    drug_class = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medications", x => x.id);
                });

            // Backfill de catálogos desde los valores DISTINCT existentes.
            // La descripción/Ndc/RxNorm/clase se toman del MAX no nulo por clave.
            migrationBuilder.Sql("""
                INSERT INTO app.allergens (id, name, created_at)
                SELECT gen_random_uuid(), allergen, now()
                FROM (SELECT DISTINCT allergen FROM app.patient_allergies) AS a;

                INSERT INTO app.icd10_codes (id, code, description, created_at)
                SELECT gen_random_uuid(), icd10_code, NULLIF(MAX(NULLIF(description, '')), ''), now()
                FROM app.patient_diagnoses
                GROUP BY icd10_code;

                INSERT INTO app.medications (id, name, ndc, rx_norm, drug_class, created_at)
                SELECT gen_random_uuid(), name,
                       NULLIF(MAX(NULLIF(ndc, '')), ''),
                       NULLIF(MAX(NULLIF(rx_norm, '')), ''),
                       NULLIF(MAX(NULLIF(drug_class, '')), ''), now()
                FROM app.patient_medications
                GROUP BY name;
                """);

            // FK a catálogos: se agregan nullable, se backfillean y luego se
            // marcan NOT NULL (patrón seguro de migración con datos existentes).
            migrationBuilder.AddColumn<Guid>(
                name: "medication_id",
                schema: "app",
                table: "patient_medications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "icd10_code_id",
                schema: "app",
                table: "patient_diagnoses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "allergen_id",
                schema: "app",
                table: "patient_allergies",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE app.patient_medications pm
                SET medication_id = m.id
                FROM app.medications m
                WHERE m.name = pm.name;

                UPDATE app.patient_diagnoses pd
                SET icd10_code_id = c.id
                FROM app.icd10_codes c
                WHERE c.code = pd.icd10_code;

                UPDATE app.patient_allergies pa
                SET allergen_id = a.id
                FROM app.allergens a
                WHERE a.name = pa.allergen;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "medication_id",
                schema: "app",
                table: "patient_medications",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "icd10_code_id",
                schema: "app",
                table: "patient_diagnoses",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "allergen_id",
                schema: "app",
                table: "patient_allergies",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // Índice único antiguo (patient_id, allergen): depende de la columna
            // allergen que se elimina al final, debe caer antes que ella.
            migrationBuilder.DropIndex(
                name: "ix_patient_allergies_patient_allergen",
                schema: "app",
                table: "patient_allergies");

            migrationBuilder.CreateIndex(
                name: "ix_patient_medications_medication_id",
                schema: "app",
                table: "patient_medications",
                column: "medication_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_diagnoses_icd10_code_id",
                schema: "app",
                table: "patient_diagnoses",
                column: "icd10_code_id");

            migrationBuilder.CreateIndex(
                name: "IX_patient_allergies_allergen_id",
                schema: "app",
                table: "patient_allergies",
                column: "allergen_id");

            migrationBuilder.CreateIndex(
                name: "ix_patient_allergies_patient_allergen",
                schema: "app",
                table: "patient_allergies",
                columns: new[] { "patient_id", "allergen_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_allergens_name",
                schema: "app",
                table: "allergens",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_icd10_codes_code",
                schema: "app",
                table: "icd10_codes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_medications_name",
                schema: "app",
                table: "medications",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_allergies_allergens_allergen_id",
                schema: "app",
                table: "patient_allergies",
                column: "allergen_id",
                principalSchema: "app",
                principalTable: "allergens",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_diagnoses_icd10_codes_icd10_code_id",
                schema: "app",
                table: "patient_diagnoses",
                column: "icd10_code_id",
                principalSchema: "app",
                principalTable: "icd10_codes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_medications_medications_medication_id",
                schema: "app",
                table: "patient_medications",
                column: "medication_id",
                principalSchema: "app",
                principalTable: "medications",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Columnas desnormalizadas (la información vive en los catálogos).
            migrationBuilder.DropColumn(
                name: "drug_class",
                schema: "app",
                table: "patient_medications");

            migrationBuilder.DropColumn(
                name: "name",
                schema: "app",
                table: "patient_medications");

            migrationBuilder.DropColumn(
                name: "ndc",
                schema: "app",
                table: "patient_medications");

            migrationBuilder.DropColumn(
                name: "rx_norm",
                schema: "app",
                table: "patient_medications");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "app",
                table: "patient_diagnoses");

            migrationBuilder.DropColumn(
                name: "icd10_code",
                schema: "app",
                table: "patient_diagnoses");

            migrationBuilder.DropColumn(
                name: "allergen",
                schema: "app",
                table: "patient_allergies");

            // Permisos para el rol de aplicación sobre los catálogos.
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.allergens TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.icd10_codes TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.medications TO app_user;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversa: se restauran las columnas originales desde los catálogos
            // antes de eliminar las FKs y las tablas de catálogo.
            migrationBuilder.AddColumn<string>(
                name: "drug_class",
                schema: "app",
                table: "patient_medications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name",
                schema: "app",
                table: "patient_medications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ndc",
                schema: "app",
                table: "patient_medications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rx_norm",
                schema: "app",
                table: "patient_medications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "app",
                table: "patient_diagnoses",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "icd10_code",
                schema: "app",
                table: "patient_diagnoses",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "allergen",
                schema: "app",
                table: "patient_allergies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE app.patient_medications pm
                SET name = m.name, ndc = m.ndc, rx_norm = m.rx_norm, drug_class = m.drug_class
                FROM app.medications m
                WHERE m.id = pm.medication_id;

                UPDATE app.patient_diagnoses pd
                SET icd10_code = c.code, description = c.description
                FROM app.icd10_codes c
                WHERE c.id = pd.icd10_code_id;

                UPDATE app.patient_allergies pa
                SET allergen = a.name
                FROM app.allergens a
                WHERE a.id = pa.allergen_id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "app",
                table: "patient_medications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "icd10_code",
                schema: "app",
                table: "patient_diagnoses",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "allergen",
                schema: "app",
                table: "patient_allergies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_patient_allergies_patient_allergen",
                schema: "app",
                table: "patient_allergies",
                columns: new[] { "patient_id", "allergen" },
                unique: true);

            migrationBuilder.DropForeignKey(
                name: "FK_patient_allergies_allergens_allergen_id",
                schema: "app",
                table: "patient_allergies");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_diagnoses_icd10_codes_icd10_code_id",
                schema: "app",
                table: "patient_diagnoses");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_medications_medications_medication_id",
                schema: "app",
                table: "patient_medications");

            migrationBuilder.DropIndex(
                name: "ix_patient_medications_medication_id",
                schema: "app",
                table: "patient_medications");

            migrationBuilder.DropIndex(
                name: "ix_patient_diagnoses_icd10_code_id",
                schema: "app",
                table: "patient_diagnoses");

            migrationBuilder.DropIndex(
                name: "IX_patient_allergies_allergen_id",
                schema: "app",
                table: "patient_allergies");

            migrationBuilder.DropIndex(
                name: "ix_patient_allergies_patient_allergen",
                schema: "app",
                table: "patient_allergies");

            migrationBuilder.DropColumn(
                name: "medication_id",
                schema: "app",
                table: "patient_medications");

            migrationBuilder.DropColumn(
                name: "icd10_code_id",
                schema: "app",
                table: "patient_diagnoses");

            migrationBuilder.DropColumn(
                name: "allergen_id",
                schema: "app",
                table: "patient_allergies");

            migrationBuilder.DropTable(
                name: "allergens",
                schema: "app");

            migrationBuilder.DropTable(
                name: "icd10_codes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "medications",
                schema: "app");
        }
    }
}
