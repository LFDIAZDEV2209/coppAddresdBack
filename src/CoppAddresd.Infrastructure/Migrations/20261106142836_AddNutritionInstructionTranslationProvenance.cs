using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    // Columna nullable sin valor por defecto ni backfill: las parejas históricas
    // conservan su texto, pero no adquieren una procedencia clínica no demostrada.
    // Down elimina solo la procedencia; un nuevo Up vuelve a exigir una pareja explícita.
    /// <inheritdoc />
    public partial class AddNutritionInstructionTranslationProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "instructions_en_source_hash",
                schema: "app",
                table: "nutrition_prescriptions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "instructions_en_source_hash",
                schema: "app",
                table: "nutrition_prescriptions");
        }
    }
}
