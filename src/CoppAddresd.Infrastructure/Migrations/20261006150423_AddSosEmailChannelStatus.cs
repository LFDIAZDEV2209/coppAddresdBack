using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSosEmailChannelStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "destination_email",
                schema: "app",
                table: "sos_alerts",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email_channel_status",
                schema: "app",
                table: "sos_alerts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pendiente");

            migrationBuilder.AddColumn<DateTime>(
                name: "email_updated_at",
                schema: "app",
                table: "sos_alerts",
                type: "timestamptz",
                nullable: true);

            // Backfill: alertas despachadas con el dispatcher viejo (que solo
            // escribía el outbox) quedan con el estado real del correo. Sin
            // esto, la fila de dedupe ya es terminal y el canal jamás se
            // re-despacha → la alerta quedaría "Pendiente" para siempre.
            migrationBuilder.Sql(
                """
                UPDATE app.sos_alerts AS a
                SET email_channel_status = CASE lower(d.email_status)
                        WHEN 'enviado' THEN 'Enviado'
                        WHEN 'fallido' THEN 'Fallido'
                        WHEN 'timeout' THEN 'Timeout'
                        WHEN 'noconfigurado' THEN 'NoConfigurado'
                        WHEN 'sindestino' THEN 'SinDestino'
                        ELSE a.email_channel_status
                    END,
                    email_updated_at = now()
                FROM app.notification_dedupe_keys AS d
                WHERE d.dedupe_key = 'sos:email:' || a.id::text
                  AND a.email_channel_status = 'Pendiente'
                  AND lower(d.email_status) IN (
                      'enviado', 'fallido', 'timeout', 'noconfigurado', 'sindestino'
                  );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "destination_email",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "email_channel_status",
                schema: "app",
                table: "sos_alerts");

            migrationBuilder.DropColumn(
                name: "email_updated_at",
                schema: "app",
                table: "sos_alerts");
        }
    }
}
