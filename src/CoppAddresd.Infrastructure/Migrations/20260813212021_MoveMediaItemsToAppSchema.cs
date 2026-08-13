using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveMediaItemsToAppSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "media_items",
                schema: "public",
                newName: "media_items",
                newSchema: "app");

            // El trigger de auditoría se mueve con la tabla; se re-adjunta sobre
            // app.media_items para dejar el attach explícito (igual que los demás módulos).
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS media_items_audit ON app.media_items;
                SELECT audit.attach_table_audit('app'::text, 'media_items'::text, 'id'::text, VARIADIC ARRAY[]::text[]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS media_items_audit ON public.media_items;
                SELECT audit.attach_table_audit('public'::text, 'media_items'::text, 'id'::text, VARIADIC ARRAY[]::text[]);
                """);

            migrationBuilder.RenameTable(
                name: "media_items",
                schema: "app",
                newName: "media_items",
                newSchema: "public");
        }
    }
}
