using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddXpRulesCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rule_code",
                schema: "app",
                table: "xp_ledger",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "xp_rules",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    base_xp = table.Column<int>(type: "integer", nullable: true),
                    multiplier = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false, defaultValue: 1.0m),
                    max_per_day = table.Column<int>(type: "integer", nullable: true),
                    max_per_week = table.Column<int>(type: "integer", nullable: true),
                    requires_validation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "CURRENT_DATE"),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xp_rules", x => x.id);
                    table.UniqueConstraint("AK_xp_rules_code", x => x.code);
                    table.CheckConstraint("ck_xp_rules_base_xp_non_negative", "\"base_xp\" IS NULL OR \"base_xp\" >= 0");
                    table.CheckConstraint("ck_xp_rules_max_limits_non_negative", "(\"max_per_day\" IS NULL OR \"max_per_day\" >= 0) AND (\"max_per_week\" IS NULL OR \"max_per_week\" >= 0)");
                    table.CheckConstraint("ck_xp_rules_multiplier_positive", "\"multiplier\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_xp_ledger_rule_code",
                schema: "app",
                table: "xp_ledger",
                column: "rule_code");

            migrationBuilder.CreateIndex(
                name: "ix_xp_rules_active",
                schema: "app",
                table: "xp_rules",
                column: "active");

            migrationBuilder.CreateIndex(
                name: "ix_xp_rules_category",
                schema: "app",
                table: "xp_rules",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "uq_xp_rules_code",
                schema: "app",
                table: "xp_rules",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_xp_ledger_xp_rules_rule_code",
                schema: "app",
                table: "xp_ledger",
                column: "rule_code",
                principalSchema: "app",
                principalTable: "xp_rules",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            // Auditoría trigger-based del catálogo (SPEC §8.5): las tablas del
            // módulo se cubren explícitamente (el trigger no es automático
            // para tablas nuevas). xp_rules es un catálogo sin PHI; el DML del
            // administrador queda auditado como el resto del módulo.
            migrationBuilder.Sql("SELECT audit.attach_table_audit('app', 'xp_rules', 'id', VARIADIC ARRAY[]::text[]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS xp_rules_audit ON app.xp_rules;");

            migrationBuilder.DropForeignKey(
                name: "FK_xp_ledger_xp_rules_rule_code",
                schema: "app",
                table: "xp_ledger");

            migrationBuilder.DropTable(
                name: "xp_rules",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_xp_ledger_rule_code",
                schema: "app",
                table: "xp_ledger");

            migrationBuilder.DropColumn(
                name: "rule_code",
                schema: "app",
                table: "xp_ledger");
        }
    }
}
