using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Telemedicine.Infrastructure.Migrations
{
    /// <summary>
    /// F3 — «Experiencia de llamada» (bloques A/B): capacidad de participante
    /// (default de BD 3 + backfill de las filas existentes en 2) y chat clínico
    /// persistido (<c>tele.chat_messages</c> + índice <c>(appointment_id, created_at)</c>).
    /// Las salas ya creadas no se tocan aquí: su capacidad se eleva de forma
    /// perezosa en el siguiente join-token/session/start (o al reabrir).
    /// </summary>
    /// <inheritdoc />
    public partial class AddRoomChatAndCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "max_participants",
                schema: "tele",
                table: "telemedicine_settings",
                type: "integer",
                nullable: false,
                defaultValue: 3,
                oldClrType: typeof(int),
                oldType: "integer");

            // Backfill F3: las filas existentes conservan el default viejo (2) y
            // pasan a 3 (profesional + paciente + 1 supervisor). Solo se tocan
            // las que aún valen 2: un valor configurado a mano (p. ej. 4) se
            // respeta.
            migrationBuilder.Sql(
                "UPDATE tele.telemedicine_settings SET max_participants = 3 WHERE max_participants = 2;");

            migrationBuilder.CreateTable(
                name: "chat_messages",
                schema: "tele",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chat_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_appointment_id_created_at",
                schema: "tele",
                table: "chat_messages",
                columns: new[] { "appointment_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_messages",
                schema: "tele");

            migrationBuilder.AlterColumn<int>(
                name: "max_participants",
                schema: "tele",
                table: "telemedicine_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 3);

            // Sin reversa del backfill: no es posible distinguir las filas
            // migradas de las configuradas a 3 manualmente.
        }
    }
}
