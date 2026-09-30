using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WidenIntakeLogCaloriesToNumeric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Widening seguro int → numeric(8,2): widening con cast implícito
            // (sin USING) y sin pérdida de datos — los valores int existentes
            // (validados 0–5000) caben en numeric(8,2). Additive: no bloquea
            // versiones anteriores (la app antigua lee decimales con .0).
            migrationBuilder.AlterColumn<decimal>(
                name: "calories",
                schema: "app",
                table: "nutrition_intake_logs",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "calories",
                schema: "app",
                table: "nutrition_intake_logs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,2)",
                oldPrecision: 8,
                oldScale: 2,
                oldNullable: true
            );
        }
    }
}
