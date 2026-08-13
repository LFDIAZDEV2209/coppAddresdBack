using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaItemTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "media_items",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    duration_secs = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_media_items_status",
                schema: "public",
                table: "media_items",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_media_items_type_status",
                schema: "public",
                table: "media_items",
                columns: new[] { "media_type", "status" });

            // Adjunta el trigger de auditoría a la nueva tabla (helper del schema audit).
            // VARIADIC explícito: con array vacío y literales unknown, PostgreSQL no
            // resuelve (42883) la llamada sin el keyword VARIADIC + casts a text.
            migrationBuilder.Sql("""
                SELECT audit.attach_table_audit('public'::text, 'media_items'::text, 'id'::text, VARIADIC ARRAY[]::text[]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS media_items_audit ON public.media_items;
                """);

            migrationBuilder.DropTable(
                name: "media_items",
                schema: "public");
        }
    }
}
