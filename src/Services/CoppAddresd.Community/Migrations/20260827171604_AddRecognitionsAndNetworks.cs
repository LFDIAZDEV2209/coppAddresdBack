using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class AddRecognitionsAndNetworks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "network_channels",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    handle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    followers = table.Column<int>(type: "integer", nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_network_channels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recognitions",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type_label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    xp = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Sent"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    triggered_by_profile_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recognitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_recognitions_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recognitions_profiles_triggered_by_profile_id",
                        column: x => x.triggered_by_profile_id,
                        principalSchema: "community",
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "network_growth_points",
                schema: "community",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_network_growth_points", x => x.id);
                    table.ForeignKey(
                        name: "FK_network_growth_points_network_channels_channel_id",
                        column: x => x.channel_id,
                        principalSchema: "community",
                        principalTable: "network_channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_network_channels_sort_order",
                schema: "community",
                table: "network_channels",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "ix_network_growth_points_channel_month",
                schema: "community",
                table: "network_growth_points",
                columns: new[] { "channel_id", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recognitions_created_at",
                schema: "community",
                table: "recognitions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_recognitions_profile_id",
                schema: "community",
                table: "recognitions",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_recognitions_triggered_by_profile_id",
                schema: "community",
                table: "recognitions",
                column: "triggered_by_profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "network_growth_points",
                schema: "community");

            migrationBuilder.DropTable(
                name: "recognitions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "network_channels",
                schema: "community");
        }
    }
}
