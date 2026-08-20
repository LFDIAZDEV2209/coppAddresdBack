using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalDocumentsAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SELECT audit.attach_table_audit('erp', 'legal_documents', 'id', VARIADIC ARRAY[]::text[]);");
            migrationBuilder.Sql("SELECT audit.attach_table_audit('erp', 'legal_document_versions', 'id', VARIADIC ARRAY[]::text[]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS legal_documents_audit ON erp.legal_documents;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS legal_document_versions_audit ON erp.legal_document_versions;");
        }
    }
}
