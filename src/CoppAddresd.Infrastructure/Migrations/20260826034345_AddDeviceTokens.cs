using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_tokens",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    platform = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_tokens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_tokens_user_id_token",
                schema: "app",
                table: "device_tokens",
                columns: new[] { "user_id", "token" },
                unique: true);

            // FK hacia auth.users (creada por SQL, patrón user_id): la tabla
            // "Users" vive en el schema auth y la gestiona el Auth Service
            // (otro DbContext). Requiere que las migraciones del Auth Service
            // ya se hayan aplicado.
            migrationBuilder.Sql("""
                ALTER TABLE app.device_tokens
                    ADD CONSTRAINT fk_device_tokens_user_id
                    FOREIGN KEY (user_id) REFERENCES auth."Users" ("Id")
                    ON DELETE CASCADE;
                """);

            // Permisos mínimos para el rol de la aplicación (convención app_user).
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON app.device_tokens TO app_user;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE app.device_tokens DROP CONSTRAINT IF EXISTS fk_device_tokens_user_id;
                """);

            migrationBuilder.DropTable(
                name: "device_tokens",
                schema: "app");
        }
    }
}
