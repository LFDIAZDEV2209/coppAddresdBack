using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuditSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "activity_logs",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    action = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    schema_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    table_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    record_id = table.Column<string>(type: "text", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    user_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    old_data = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    new_data = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    changed_data = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    metadata = table.Column<JsonElement>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_logs_occurred_at",
                schema: "audit",
                table: "activity_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_activity_logs_table_record",
                schema: "audit",
                table: "activity_logs",
                columns: new[] { "table_name", "record_id" });

            // ------------------------------------------------------------------
            // Función genérica del trigger de auditoría.
            // Coste por escritura: cero queries a catálogo, cero joins, cero
            // dynamic SQL; solo manipulación jsonb de OLD/NEW en memoria.
            // El actor llega vía GUC transaccionales (set_config con is_local=true)
            // que el interceptor .NET (AuditTriggerInterceptor) propaga tras BEGIN;
            // al morir con la transacción, el connection pooling no filtra actores.
            // Columnas sensibles se excluyen por tabla con el variadic de la
            // creación del trigger (p. ej. 'password_hash').
            // ------------------------------------------------------------------
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION audit.audit_trigger_function()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    -- TG_ARGV[0] = columna PK; TG_ARGV[1..] = columnas sensibles a excluir
                    v_pk text := CASE WHEN TG_NARGS > 0 THEN TG_ARGV[0] ELSE 'id' END;
                    v_excluded text[] := TG_ARGV[1:TG_NARGS];
                    v_old jsonb;
                    v_new jsonb;
                    v_changed jsonb;
                    v_record_id text;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        v_old := to_jsonb(OLD);
                        v_record_id := NULLIF(v_old ->> v_pk, '');
                    ELSE
                        v_new := to_jsonb(NEW);
                        v_record_id := NULLIF(v_new ->> v_pk, '');
                        IF TG_OP = 'UPDATE' THEN
                            v_old := to_jsonb(OLD);
                        END IF;
                    END IF;

                    -- Remueve columnas sensibles de los payloads jsonb
                    v_old := v_old - v_excluded;
                    v_new := v_new - v_excluded;

                    -- Solo columnas que realmente cambiaron (UPDATE)
                    IF TG_OP = 'UPDATE' AND v_old IS NOT NULL AND v_new IS NOT NULL THEN
                        SELECT jsonb_object_agg(e.k, e.v)
                          INTO v_changed
                          FROM jsonb_each(v_new) e(k, v)
                         WHERE NOT v_old @> jsonb_build_object(e.k, e.v);
                    END IF;

                    INSERT INTO audit.activity_logs
                        (id, occurred_at, action, schema_name, table_name, record_id,
                         actor_type, user_id, user_email, user_role,
                         ip_address, request_id, correlation_id,
                         old_data, new_data, changed_data, metadata)
                    VALUES
                        (gen_random_uuid(), clock_timestamp(), TG_OP,
                         TG_TABLE_SCHEMA, TG_TABLE_NAME, v_record_id,
                         COALESCE(NULLIF(current_setting('audit.actor_type', true), ''), 'SYSTEM'),
                         NULLIF(current_setting('audit.user_id', true), '')::uuid,
                         NULLIF(current_setting('audit.user_email', true), ''),
                         NULLIF(current_setting('audit.user_role', true), ''),
                         NULLIF(current_setting('audit.ip_address', true), ''),
                         NULLIF(current_setting('audit.request_id', true), ''),
                         NULLIF(current_setting('audit.correlation_id', true), ''),
                         v_old, v_new, v_changed, NULL);
                    RETURN NULL;
                END;
                $$;
                """);

            // Helper de despliegue: adjunta el trigger a una tabla (solo tiempo de
            // despliegue; coste por escritura cero). p_pk = columna PK, p_excluded =
            // columnas sensibles a omitir de old_data/new_data/changed_data.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION audit.attach_table_audit(
                    p_schema text,
                    p_table text,
                    p_pk text,
                    VARIADIC p_excluded text[])
                RETURNS void
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    v_extra text := '';
                BEGIN
                    IF cardinality(p_excluded) > 0 THEN
                        SELECT string_agg(quote_literal(c), ', ')
                          INTO v_extra
                          FROM unnest(p_excluded) AS c;
                        v_extra := ', ' || v_extra;
                    END IF;

                    EXECUTE format(
                        'CREATE TRIGGER %I AFTER INSERT OR UPDATE OR DELETE ON %I.%I ' ||
                        'FOR EACH ROW EXECUTE FUNCTION audit.audit_trigger_function(%L%s)',
                        p_table || '_audit', p_schema, p_table, p_pk, v_extra);
                END;
                $$;
                """);

            // Permisos mínimos para el rol de la aplicación (dev y producción usan
            // la convención app_user; en producción sin superusuario).
            migrationBuilder.Sql("""
                GRANT USAGE ON SCHEMA audit TO app_user;
                GRANT SELECT, INSERT ON audit.activity_logs TO app_user;
                GRANT EXECUTE ON FUNCTION audit.audit_trigger_function() TO app_user;
                GRANT EXECUTE ON FUNCTION audit.attach_table_audit(text, text, text, text[]) TO app_user;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS audit.attach_table_audit(text, text, text, text[]);
                DROP FUNCTION IF EXISTS audit.audit_trigger_function();
                """);

            migrationBuilder.DropTable(
                name: "activity_logs",
                schema: "audit");

            migrationBuilder.Sql("DROP SCHEMA IF EXISTS audit CASCADE;");
        }
    }
}
