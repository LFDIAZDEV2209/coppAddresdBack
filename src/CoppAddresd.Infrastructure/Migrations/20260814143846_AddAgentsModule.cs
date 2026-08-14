using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "agents");

            migrationBuilder.CreateTable(
                name: "agent_instances",
                schema: "agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_instances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_type_versions",
                schema: "agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    agent_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_type_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_types",
                schema: "agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    specialty = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    icon_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    active_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_types", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_types_active_version",
                        column: x => x.active_version_id,
                        principalSchema: "agents",
                        principalTable: "agent_type_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_bases",
                schema: "agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    agent_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_bases", x => x.id);
                    table.ForeignKey(
                        name: "fk_knowledge_bases_agent_type",
                        column: x => x.agent_type_id,
                        principalSchema: "agents",
                        principalTable: "agent_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    knowledge_base_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    chunks_count = table.Column<int>(type: "integer", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_documents_knowledge_bases_knowledge_base_id",
                        column: x => x.knowledge_base_id,
                        principalSchema: "agents",
                        principalTable: "knowledge_bases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_instances_agent_type_id",
                schema: "agents",
                table: "agent_instances",
                column: "agent_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_instances_user_id_agent_type_id",
                schema: "agents",
                table: "agent_instances",
                columns: new[] { "user_id", "agent_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_type_versions_active_per_type",
                schema: "agents",
                table: "agent_type_versions",
                column: "agent_type_id",
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "ix_agent_type_versions_agent_type_id_version_number",
                schema: "agents",
                table: "agent_type_versions",
                columns: new[] { "agent_type_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_types_active_version_id",
                schema: "agents",
                table: "agent_types",
                column: "active_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_knowledge_base_id",
                schema: "agents",
                table: "documents",
                column: "knowledge_base_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_bases_agent_type_id",
                schema: "agents",
                table: "knowledge_bases",
                column: "agent_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_bases_scope",
                schema: "agents",
                table: "knowledge_bases",
                column: "scope");

            migrationBuilder.AddForeignKey(
                name: "fk_agent_instances_agent_type",
                schema: "agents",
                table: "agent_instances",
                column: "agent_type_id",
                principalSchema: "agents",
                principalTable: "agent_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_agent_type_versions_agent_types_agent_type_id",
                schema: "agents",
                table: "agent_type_versions",
                column: "agent_type_id",
                principalSchema: "agents",
                principalTable: "agent_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // ------------------------------------------------------------------
            // FK hacia auth.users: la tabla "Users" vive en el schema auth y la
            // gestiona el Auth Service (otro DbContext). La restricción se crea
            // aquí por SQL. Requiere que las migraciones del Auth Service ya se
            // hayan aplicado (el Auth Service las aplica automáticamente al
            // iniciar).
            // ------------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE agents.agent_instances
                    ADD CONSTRAINT fk_agent_instances_user_id
                    FOREIGN KEY (user_id) REFERENCES auth."Users" ("Id")
                    ON DELETE CASCADE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE agents.agent_instances
                    DROP CONSTRAINT fk_agent_instances_user_id;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_agent_type_versions_agent_types_agent_type_id",
                schema: "agents",
                table: "agent_type_versions");

            migrationBuilder.DropTable(
                name: "agent_instances",
                schema: "agents");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "agents");

            migrationBuilder.DropTable(
                name: "knowledge_bases",
                schema: "agents");

            migrationBuilder.DropTable(
                name: "agent_types",
                schema: "agents");

            migrationBuilder.DropTable(
                name: "agent_type_versions",
                schema: "agents");
        }
    }
}
