using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRedesModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "facturas_rips",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cuv = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    factura_numero = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    prestador_nit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prestador_razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fecha_radicacion = table.Column<DateTime>(type: "date", nullable: false),
                    valor_total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    valor_copago = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    valor_cuota_moderadora = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    valor_neto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    usuario_tipo = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    usuario_documento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    usuario_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    numero_contrato = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    modalidad_contrato = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cobertura = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    periodo_atencion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    estado = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    registros = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facturas_rips", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "radicaciones",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    consecutivo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    diagnostico_cie10 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    diagnostico_descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    prioridad = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cotizacion_nombre_archivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cotizacion_numero = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cotizacion_fecha = table.Column<DateTime>(type: "date", nullable: true),
                    cotizacion_monto = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    estado = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_radicaciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "redes_prestadoras",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    nit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    direccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ciudad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    departamento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    codigo_prestador = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    habilitacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    naturaleza = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_redes_prestadoras", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "facturas_rips_archivos",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    factura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    registros = table.Column<int>(type: "integer", nullable: false),
                    tamano = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facturas_rips_archivos", x => x.id);
                    table.ForeignKey(
                        name: "FK_facturas_rips_archivos_facturas_rips_factura_id",
                        column: x => x.factura_id,
                        principalSchema: "erp",
                        principalTable: "facturas_rips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "radicacion_cups",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    radicacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cantidad = table.Column<int>(type: "integer", nullable: false),
                    valor_unitario = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_radicacion_cups", x => x.id);
                    table.ForeignKey(
                        name: "FK_radicacion_cups_radicaciones_radicacion_id",
                        column: x => x.radicacion_id,
                        principalSchema: "erp",
                        principalTable: "radicaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_facturas_rips_cuv",
                schema: "erp",
                table: "facturas_rips",
                column: "cuv",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_facturas_rips_estado",
                schema: "erp",
                table: "facturas_rips",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_facturas_rips_prestador_nit",
                schema: "erp",
                table: "facturas_rips",
                column: "prestador_nit");

            migrationBuilder.CreateIndex(
                name: "ix_facturas_rips_archivos_factura_id",
                schema: "erp",
                table: "facturas_rips_archivos",
                column: "factura_id");

            migrationBuilder.CreateIndex(
                name: "ix_radicacion_cups_radicacion_id",
                schema: "erp",
                table: "radicacion_cups",
                column: "radicacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_radicaciones_consecutivo",
                schema: "erp",
                table: "radicaciones",
                column: "consecutivo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_radicaciones_estado",
                schema: "erp",
                table: "radicaciones",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_radicaciones_nit",
                schema: "erp",
                table: "radicaciones",
                column: "nit");

            migrationBuilder.CreateIndex(
                name: "ix_redes_prestadoras_nit",
                schema: "erp",
                table: "redes_prestadoras",
                column: "nit",
                unique: true);

            // GRANT estándar del módulo (patrón NormalizeClinicalCatalogs).
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.redes_prestadoras TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.radicaciones TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.radicacion_cups TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.facturas_rips TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON erp.facturas_rips_archivos TO app_user;
                """);

            // Catálogo de redes prestadoras (subset demo del acceso a redes;
            // idempotente por NIT para crecer sin romper re-ejecuciones).
            migrationBuilder.Sql("""
                INSERT INTO erp.redes_prestadoras
                    (nit, razon_social, direccion, ciudad, departamento, telefono, email, codigo_prestador, habilitacion, naturaleza)
                VALUES
                    ('900123456-1', 'Clínica Santa Bárbara S.A.S.', 'Cra. 43A # 18B-20, El Poblado', 'Medellín', 'Antioquia', '(604) 312 88 45', 'contratacion@clinicasantabarbara.co', '03001014001', 'habilitado', 'privada'),
                    ('830112345-2', 'Hospital San Rafael de Envigado', 'Cra. 43 # 26-90', 'Envigado', 'Antioquia', '(604) 339 50 00', 'autorizaciones@hsre.gov.co', '03001028001', 'habilitado', 'publica'),
                    ('901234567-8', 'Centro Médico Imbanaco', 'Calle 5 # 60-38', 'Cali', 'Valle del Cauca', '(602) 664 30 00', 'redimbanaco@imbanaco.gov.co', '76001201001', 'habilitado', 'mixta'),
                    ('860012345-6', 'Clínica del Country', 'Calle 34 # 5-35', 'Bogotá', 'Cundinamarca', '(601) 375 66 00', 'autorizaciones@clinicadelcountry.com', '11001013001', 'habilitado', 'privada'),
                    ('800456789-3', 'Medicall Health Services', 'Av. El Dorado # 68-90', 'Bogotá', 'Cundinamarca', '(601) 411 20 00', 'red@medicall.co', '11001045001', 'habilitado', 'privada'),
                    ('890998877-4', 'Clínica Versalles', 'Calle 62 # 15-40', 'Barranquilla', 'Atlántico', '(605) 386 20 30', 'gestion@clinicaversalles.co', '08001022001', 'no-habilitado', 'privada')
                ON CONFLICT (nit) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "facturas_rips_archivos",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "radicacion_cups",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "redes_prestadoras",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "facturas_rips",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "radicaciones",
                schema: "erp");
        }    }
}
