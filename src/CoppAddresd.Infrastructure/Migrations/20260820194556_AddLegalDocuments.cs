using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "legal_documents",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legal_document_versions",
                schema: "erp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    major = table.Column<int>(type: "integer", nullable: false),
                    minor = table.Column<int>(type: "integer", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_document_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_legal_document_versions_legal_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "erp",
                        principalTable: "legal_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_legal_document_versions_document_version",
                schema: "erp",
                table: "legal_document_versions",
                columns: new[] { "document_id", "major", "minor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_legal_documents_code",
                schema: "erp",
                table: "legal_documents",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_legal_documents_current_version_id",
                schema: "erp",
                table: "legal_documents",
                column: "current_version_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legal_document_versions",
                schema: "erp");

            migrationBuilder.DropTable(
                name: "legal_documents",
                schema: "erp");
        }
    }
}
