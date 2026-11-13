using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaSourceAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_url",
                schema: "app",
                table: "media_items",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_kind",
                schema: "app",
                table: "media_items",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "file");

            migrationBuilder.AddColumn<string>(
                name: "tags",
                schema: "app",
                table: "media_items",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
            // Columnas con defaults constantes: ampliación por metadatos, sin backfill.
            // CONCURRENTLY confirma primero el DDL corto y evita bloquear escrituras al indexar.
            migrationBuilder.Sql("CREATE INDEX CONCURRENTLY ix_media_items_tags ON app.media_items USING gin (tags jsonb_path_ops);", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE app.media_items ADD CONSTRAINT ck_media_items_source_kind CHECK (source_kind IN ('file','external')) NOT VALID; ALTER TABLE app.media_items ADD CONSTRAINT ck_media_items_tags_array CHECK (jsonb_typeof(tags)='array') NOT VALID;", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE app.media_items VALIDATE CONSTRAINT ck_media_items_source_kind; ALTER TABLE app.media_items VALIDATE CONSTRAINT ck_media_items_tags_array;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversa en ventana sin escritores: exportar y retirar/restablecer los
            // metadatos nuevos antes. El bloqueo y la comprobación son atómicos;
            // nunca se descartan enlaces ni etiquetas automáticamente.
            migrationBuilder.Sql("""
                LOCK TABLE app.media_items IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM app.media_items WHERE source_kind <> 'file' OR external_url IS NOT NULL OR tags <> '[]'::jsonb) THEN
                        RAISE EXCEPTION 'Reversa requiere exportar y retirar enlaces/etiquetas nuevos';
                    END IF;
                END $$;
                DROP INDEX app.ix_media_items_tags;
                ALTER TABLE app.media_items DROP CONSTRAINT ck_media_items_source_kind;
                ALTER TABLE app.media_items DROP CONSTRAINT ck_media_items_tags_array;
                """);

            migrationBuilder.DropColumn(
                name: "external_url",
                schema: "app",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "source_kind",
                schema: "app",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "tags",
                schema: "app",
                table: "media_items");
        }
    }
}
