using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Columnas FK nuevas en patient_profiles (antes del backfill).
            migrationBuilder.AddColumn<Guid>(
                name: "blood_type_id",
                schema: "app",
                table: "patient_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "city_id",
                schema: "app",
                table: "patient_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "country_id",
                schema: "app",
                table: "patient_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "document_type_id",
                schema: "app",
                table: "patient_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ethnicity_id",
                schema: "app",
                table: "patient_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_country_code",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "state_id",
                schema: "app",
                table: "patient_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "marital_status",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            // 2. Tablas de catálogo.
            migrationBuilder.CreateTable(
                name: "blood_types",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blood_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "countries",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_types",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ethnicities",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ethnicities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "states",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    country_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_states", x => x.id);
                    table.ForeignKey(
                        name: "FK_states_countries_country_id",
                        column: x => x.country_id,
                        principalSchema: "app",
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cities",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cities", x => x.id);
                    table.ForeignKey(
                        name: "FK_cities_states_state_id",
                        column: x => x.state_id,
                        principalSchema: "app",
                        principalTable: "states",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "postal_codes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zip_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_postal_codes", x => x.id);
                    table.ForeignKey(
                        name: "FK_postal_codes_cities_city_id",
                        column: x => x.city_id,
                        principalSchema: "app",
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // 3. Índices de catálogos y FKs.
            migrationBuilder.CreateIndex(
                name: "ix_blood_types_code",
                schema: "app",
                table: "blood_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cities_state_id",
                schema: "app",
                table: "cities",
                column: "state_id");

            migrationBuilder.CreateIndex(
                name: "ix_cities_state_name",
                schema: "app",
                table: "cities",
                columns: new[] { "state_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_countries_code",
                schema: "app",
                table: "countries",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_types_code",
                schema: "app",
                table: "document_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ethnicities_code",
                schema: "app",
                table: "ethnicities",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_postal_codes_city_id",
                schema: "app",
                table: "postal_codes",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_postal_codes_city_zip",
                schema: "app",
                table: "postal_codes",
                columns: new[] { "city_id", "zip_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_states_country_code",
                schema: "app",
                table: "states",
                columns: new[] { "country_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_states_country_id",
                schema: "app",
                table: "states",
                column: "country_id");

            // 4. Seed de catálogos (recurso embebido, idempotente).
            migrationBuilder.Sql(ReadSeedScript());

            // 5. Autocomplete con pg_trgm sobre los catálogos clínicos y ciudades.
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS pg_trgm;

                CREATE INDEX IF NOT EXISTS ix_icd10_codes_code_trgm
                    ON app.icd10_codes USING gin (code gin_trgm_ops);
                CREATE INDEX IF NOT EXISTS ix_icd10_codes_description_trgm
                    ON app.icd10_codes USING gin (description gin_trgm_ops);
                CREATE INDEX IF NOT EXISTS ix_medications_name_trgm
                    ON app.medications USING gin (name gin_trgm_ops);
                CREATE INDEX IF NOT EXISTS ix_medications_ndc_trgm
                    ON app.medications USING gin (ndc gin_trgm_ops);
                CREATE INDEX IF NOT EXISTS ix_allergens_name_trgm
                    ON app.allergens USING gin (name gin_trgm_ops);
                CREATE INDEX IF NOT EXISTS ix_cities_name_trgm
                    ON app.cities USING gin (name gin_trgm_ops);
                """);

            // 6. FKs de patient_profiles hacia los catálogos.
            migrationBuilder.AddForeignKey(
                name: "FK_patient_profiles_blood_types_blood_type_id",
                schema: "app",
                table: "patient_profiles",
                column: "blood_type_id",
                principalSchema: "app",
                principalTable: "blood_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_profiles_cities_city_id",
                schema: "app",
                table: "patient_profiles",
                column: "city_id",
                principalSchema: "app",
                principalTable: "cities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_profiles_countries_country_id",
                schema: "app",
                table: "patient_profiles",
                column: "country_id",
                principalSchema: "app",
                principalTable: "countries",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_profiles_document_types_document_type_id",
                schema: "app",
                table: "patient_profiles",
                column: "document_type_id",
                principalSchema: "app",
                principalTable: "document_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_profiles_ethnicities_ethnicity_id",
                schema: "app",
                table: "patient_profiles",
                column: "ethnicity_id",
                principalSchema: "app",
                principalTable: "ethnicities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_patient_profiles_states_state_id",
                schema: "app",
                table: "patient_profiles",
                column: "state_id",
                principalSchema: "app",
                principalTable: "states",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // 7. Índices de las FKs nuevas de patient_profiles.
            migrationBuilder.CreateIndex(
                name: "IX_patient_profiles_blood_type_id",
                schema: "app",
                table: "patient_profiles",
                column: "blood_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_patient_profiles_city_id",
                schema: "app",
                table: "patient_profiles",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "IX_patient_profiles_country_id",
                schema: "app",
                table: "patient_profiles",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "IX_patient_profiles_document_type_id",
                schema: "app",
                table: "patient_profiles",
                column: "document_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_patient_profiles_ethnicity_id",
                schema: "app",
                table: "patient_profiles",
                column: "ethnicity_id");

            migrationBuilder.CreateIndex(
                name: "IX_patient_profiles_state_id",
                schema: "app",
                table: "patient_profiles",
                column: "state_id");

            // 8. Columnas legacy eliminadas (el dato ya migró a las FKs).
            migrationBuilder.DropColumn(
                name: "blood_type",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "city",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "document_type",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "ethnicity",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "phone",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "state",
                schema: "app",
                table: "patient_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_patient_profiles_blood_types_blood_type_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_profiles_cities_city_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_profiles_countries_country_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_profiles_document_types_document_type_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_profiles_ethnicities_ethnicity_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_patient_profiles_states_state_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropIndex(
                name: "IX_patient_profiles_blood_type_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropIndex(
                name: "IX_patient_profiles_city_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropIndex(
                name: "IX_patient_profiles_country_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropIndex(
                name: "IX_patient_profiles_document_type_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropIndex(
                name: "IX_patient_profiles_ethnicity_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropIndex(
                name: "IX_patient_profiles_state_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "blood_type_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "city_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "country_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "document_type_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "ethnicity_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "marital_status",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "phone_country_code",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "phone_number",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "state_id",
                schema: "app",
                table: "patient_profiles");

            migrationBuilder.DropTable(
                name: "blood_types",
                schema: "app");

            migrationBuilder.DropTable(
                name: "document_types",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ethnicities",
                schema: "app");

            migrationBuilder.DropTable(
                name: "postal_codes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "cities",
                schema: "app");

            migrationBuilder.DropTable(
                name: "states",
                schema: "app");

            migrationBuilder.DropTable(
                name: "countries",
                schema: "app");

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_icd10_codes_code_trgm;
                DROP INDEX IF EXISTS ix_icd10_codes_description_trgm;
                DROP INDEX IF EXISTS ix_medications_name_trgm;
                DROP INDEX IF EXISTS ix_medications_ndc_trgm;
                DROP INDEX IF EXISTS ix_allergens_name_trgm;
                DROP INDEX IF EXISTS ix_cities_name_trgm;
                """);

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
                name: "document_type",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ethnicity",
                schema: "app",
                table: "patient_profiles",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
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
        }

        /// <summary>Lee el seed de catálogos embebido (recurso EmbeddedResource).</summary>
        private static string ReadSeedScript()
        {
            var assembly = typeof(AddPatientCatalogs).Assembly;
            using var stream = assembly.GetManifestResourceStream(
                    "CoppAddresd.Infrastructure.Migrations.Seed.AddPatientCatalogs.sql")
                ?? throw new InvalidOperationException(
                    "Recurso embebido 'AddPatientCatalogs.sql' no encontrado.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}