using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalConsentReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "legal_document_acceptances",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_document_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    locale = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    disclosure_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    disclosure_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    withdrawn_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_document_acceptances", x => x.id);
                    table.ForeignKey(
                        name: "FK_legal_document_acceptances_legal_document_versions_legal_do~",
                        column: x => x.legal_document_version_id,
                        principalSchema: "erp",
                        principalTable: "legal_document_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_legal_acceptance_user_scope_version",
                schema: "app",
                table: "legal_document_acceptances",
                columns: new[] { "user_id", "scope", "legal_document_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_legal_document_acceptances_legal_document_version_id",
                schema: "app",
                table: "legal_document_acceptances",
                column: "legal_document_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_legal_acceptance_user_request",
                schema: "app",
                table: "legal_document_acceptances",
                columns: new[] { "user_id", "idempotency_key" },
                unique: true);

            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'legal_document_acceptances', 'id', VARIADIC ARRAY[]::text[]);");
            // A receipt's referenced published text must remain an immutable snapshot.
            migrationBuilder.Sql("""
                CREATE FUNCTION erp.protect_published_legal_version() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD.is_published THEN
                        RAISE EXCEPTION 'Published legal versions are immutable.' USING ERRCODE = '23514';
                    END IF;
                    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER protect_published_legal_version
                    BEFORE UPDATE OR DELETE ON erp.legal_document_versions
                    FOR EACH ROW EXECUTE FUNCTION erp.protect_published_legal_version();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS protect_published_legal_version ON erp.legal_document_versions; DROP FUNCTION IF EXISTS erp.protect_published_legal_version();");
            migrationBuilder.DropTable(
                name: "legal_document_acceptances",
                schema: "app");
        }
    }
}
