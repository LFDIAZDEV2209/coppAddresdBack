using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessionalOrganizationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "erp",
                table: "employees",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "erp",
                table: "employees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "erp",
                table: "employees",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "first_name",
                schema: "erp",
                table: "employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                schema: "erp",
                table: "employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "middle_name",
                schema: "erp",
                table: "employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                schema: "erp",
                table: "employees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "phone_country_code",
                schema: "erp",
                table: "employees",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                schema: "erp",
                table: "employees",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "erp",
                table: "employees",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "professional_types",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_professional_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "specialties",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_specialties", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clinics",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinics", x => x.id);
                    table.ForeignKey(
                        name: "FK_clinics_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "erp",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "professionals",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bio = table.Column<string>(type: "text", nullable: true),
                    photo_storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    onboarding_completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_professionals", x => x.id);
                    table.ForeignKey(
                        name: "FK_professionals_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "erp",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_professionals_professional_types_professional_type_id",
                        column: x => x.professional_type_id,
                        principalSchema: "erp",
                        principalTable: "professional_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "professional_type_specialties",
                schema: "erp",
                columns: table => new
                {
                    professional_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialty_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_professional_type_specialties", x => new { x.professional_type_id, x.specialty_id });
                    table.ForeignKey(
                        name: "FK_professional_type_specialties_professional_types_profession~",
                        column: x => x.professional_type_id,
                        principalSchema: "erp",
                        principalTable: "professional_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_professional_type_specialties_specialties_specialty_id",
                        column: x => x.specialty_id,
                        principalSchema: "erp",
                        principalTable: "specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_clinics",
                schema: "erp",
                columns: table => new
                {
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_clinics", x => new { x.employee_id, x.clinic_id });
                    table.ForeignKey(
                        name: "FK_employee_clinics_clinics_clinic_id",
                        column: x => x.clinic_id,
                        principalSchema: "erp",
                        principalTable: "clinics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_clinics_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "erp",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    clinic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address_line_1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_line_2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state_id = table.Column<Guid>(type: "uuid", nullable: true),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    phone_country_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.id);
                    table.ForeignKey(
                        name: "FK_locations_cities_city_id",
                        column: x => x.city_id,
                        principalSchema: "app",
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_locations_clinics_clinic_id",
                        column: x => x.clinic_id,
                        principalSchema: "erp",
                        principalTable: "clinics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_locations_states_state_id",
                        column: x => x.state_id,
                        principalSchema: "app",
                        principalTable: "states",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "professional_licenses",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    license_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    specialty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    state_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    issued_at = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_at = table.Column<DateOnly>(type: "date", nullable: true),
                    verification_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_professional_licenses", x => x.id);
                    table.ForeignKey(
                        name: "FK_professional_licenses_professionals_professional_id",
                        column: x => x.professional_id,
                        principalSchema: "erp",
                        principalTable: "professionals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_professional_licenses_specialties_specialty_id",
                        column: x => x.specialty_id,
                        principalSchema: "erp",
                        principalTable: "specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "professional_specialties",
                schema: "erp",
                columns: table => new
                {
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_professional_specialties", x => new { x.professional_id, x.specialty_id });
                    table.ForeignKey(
                        name: "FK_professional_specialties_professionals_professional_id",
                        column: x => x.professional_id,
                        principalSchema: "erp",
                        principalTable: "professionals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_professional_specialties_specialties_specialty_id",
                        column: x => x.specialty_id,
                        principalSchema: "erp",
                        principalTable: "specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "professional_locations",
                schema: "erp",
                columns: table => new
                {
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_professional_locations", x => new { x.professional_id, x.location_id });
                    table.ForeignKey(
                        name: "FK_professional_locations_locations_location_id",
                        column: x => x.location_id,
                        principalSchema: "erp",
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_professional_locations_professionals_professional_id",
                        column: x => x.professional_id,
                        principalSchema: "erp",
                        principalTable: "professionals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employees_organization_email",
                schema: "erp",
                table: "employees",
                columns: new[] { "organization_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employees_organization_id",
                schema: "erp",
                table: "employees",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_employees_status",
                schema: "erp",
                table: "employees",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_clinics_organization_id",
                schema: "erp",
                table: "clinics",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinics_organization_name",
                schema: "erp",
                table: "clinics",
                columns: new[] { "organization_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employee_clinics_clinic_id",
                schema: "erp",
                table: "employee_clinics",
                column: "clinic_id");

            migrationBuilder.CreateIndex(
                name: "ix_locations_city_id",
                schema: "erp",
                table: "locations",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_locations_clinic_id",
                schema: "erp",
                table: "locations",
                column: "clinic_id");

            migrationBuilder.CreateIndex(
                name: "ix_locations_clinic_name",
                schema: "erp",
                table: "locations",
                columns: new[] { "clinic_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_locations_state_id",
                schema: "erp",
                table: "locations",
                column: "state_id");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_code",
                schema: "erp",
                table: "organizations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_professional_licenses_professional_id",
                schema: "erp",
                table: "professional_licenses",
                column: "professional_id");

            migrationBuilder.CreateIndex(
                name: "ix_professional_licenses_specialty_id",
                schema: "erp",
                table: "professional_licenses",
                column: "specialty_id");

            migrationBuilder.CreateIndex(
                name: "ix_professional_licenses_type_number",
                schema: "erp",
                table: "professional_licenses",
                columns: new[] { "professional_id", "license_type", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_professional_locations_location_id",
                schema: "erp",
                table: "professional_locations",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_professional_specialties_specialty_id",
                schema: "erp",
                table: "professional_specialties",
                column: "specialty_id");

            migrationBuilder.CreateIndex(
                name: "ix_professional_type_specialties_specialty_id",
                schema: "erp",
                table: "professional_type_specialties",
                column: "specialty_id");

            migrationBuilder.CreateIndex(
                name: "ix_professional_types_code",
                schema: "erp",
                table: "professional_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_professionals_employee_id",
                schema: "erp",
                table: "professionals",
                column: "employee_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_professionals_professional_type_id",
                schema: "erp",
                table: "professionals",
                column: "professional_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_specialties_category_sort",
                schema: "erp",
                table: "specialties",
                columns: new[] { "category", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_specialties_code",
                schema: "erp",
                table: "specialties",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_employees_organizations_organization_id",
                schema: "erp",
                table: "employees",
                column: "organization_id",
                principalSchema: "erp",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Permisos del rol de la aplicación sobre las tablas nuevas del
            // schema erp (convención app_user, igual que AddAppAndErpSchemas).
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.organizations TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.clinics TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.locations TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.professional_types TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.specialties TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.professional_type_specialties TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.professionals TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.employee_clinics TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.professional_locations TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.professional_specialties TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.professional_licenses TO app_user;
                """);

            // Seed de catálogos profesionales (recurso embebido, idempotente).
            migrationBuilder.Sql(ReadSeedScript());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_employees_organizations_organization_id",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropTable(
                name: "employee_clinics",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "professional_licenses",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "professional_locations",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "professional_specialties",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "professional_type_specialties",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "locations",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "professionals",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "specialties",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "clinics",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "professional_types",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "erp");

            migrationBuilder.DropIndex(
                name: "ix_employees_organization_email",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "ix_employees_organization_id",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "ix_employees_status",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "first_name",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "last_name",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "middle_name",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "organization_id",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "phone_country_code",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "phone_number",
                schema: "erp",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "erp",
                table: "employees");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "erp",
                table: "employees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <summary>Lee el seed de catálogos profesionales embebido (recurso EmbeddedResource).</summary>
        private static string ReadSeedScript()
        {
            var assembly = typeof(AddProfessionalOrganizationSchema).Assembly;
            using var stream = assembly.GetManifestResourceStream(
                    "CoppAddresd.Infrastructure.Migrations.Seed.AddProfessionalCatalogs.sql")
                ?? throw new InvalidOperationException(
                    "Recurso embebido 'AddProfessionalCatalogs.sql' no encontrado.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
