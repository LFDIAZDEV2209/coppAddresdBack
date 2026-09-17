using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBilingualNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "subject",
                schema: "app",
                table: "health_test_notification_templates",
                newName: "subject_es");

            migrationBuilder.RenameColumn(
                name: "name",
                schema: "app",
                table: "health_test_notification_templates",
                newName: "name_es");

            migrationBuilder.RenameColumn(
                name: "body_template",
                schema: "app",
                table: "health_test_notification_templates",
                newName: "body_template_es");

            migrationBuilder.RenameColumn(
                name: "subject",
                schema: "app",
                table: "health_test_notification_template_versions",
                newName: "subject_es");

            migrationBuilder.RenameColumn(
                name: "name",
                schema: "app",
                table: "health_test_notification_template_versions",
                newName: "name_es");

            migrationBuilder.RenameColumn(
                name: "body_template",
                schema: "app",
                table: "health_test_notification_template_versions",
                newName: "body_template_es");

            migrationBuilder.AddColumn<string>(
                name: "language",
                schema: "app",
                table: "health_test_notifications",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "es");

            migrationBuilder.AddColumn<string>(
                name: "body_template_en",
                schema: "app",
                table: "health_test_notification_templates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_en",
                schema: "app",
                table: "health_test_notification_templates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subject_en",
                schema: "app",
                table: "health_test_notification_templates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "body_template_en",
                schema: "app",
                table: "health_test_notification_template_versions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_en",
                schema: "app",
                table: "health_test_notification_template_versions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subject_en",
                schema: "app",
                table: "health_test_notification_template_versions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Las plantillas de las reglas de alerta seguían usando el formato
            // anterior ({value}/{label}) que el renderizador ya no reemplaza;
            // se convierten a las claves soportadas en corchetes.
            migrationBuilder.Sql(
                """
                UPDATE app.health_test_alert_rules
                SET message_template = replace(message_template, '{value}', '[valor]')
                WHERE message_template LIKE '%{value}%';

                UPDATE app.health_test_alert_rules
                SET message_template = replace(message_template, '{label}', '[indicador]')
                WHERE message_template LIKE '%{label}%';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revierte el formato de las plantillas de reglas al anterior.
            migrationBuilder.Sql(
                """
                UPDATE app.health_test_alert_rules
                SET message_template = replace(message_template, '[valor]', '{value}')
                WHERE message_template LIKE '%[valor]%';

                UPDATE app.health_test_alert_rules
                SET message_template = replace(message_template, '[indicador]', '{label}')
                WHERE message_template LIKE '%[indicador]%';
                """
            );

            migrationBuilder.DropColumn(
                name: "language",
                schema: "app",
                table: "health_test_notifications");

            migrationBuilder.DropColumn(
                name: "body_template_en",
                schema: "app",
                table: "health_test_notification_templates");

            migrationBuilder.DropColumn(
                name: "name_en",
                schema: "app",
                table: "health_test_notification_templates");

            migrationBuilder.DropColumn(
                name: "subject_en",
                schema: "app",
                table: "health_test_notification_templates");

            migrationBuilder.DropColumn(
                name: "body_template_en",
                schema: "app",
                table: "health_test_notification_template_versions");

            migrationBuilder.DropColumn(
                name: "name_en",
                schema: "app",
                table: "health_test_notification_template_versions");

            migrationBuilder.DropColumn(
                name: "subject_en",
                schema: "app",
                table: "health_test_notification_template_versions");

            migrationBuilder.RenameColumn(
                name: "subject_es",
                schema: "app",
                table: "health_test_notification_templates",
                newName: "subject");

            migrationBuilder.RenameColumn(
                name: "name_es",
                schema: "app",
                table: "health_test_notification_templates",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "body_template_es",
                schema: "app",
                table: "health_test_notification_templates",
                newName: "body_template");

            migrationBuilder.RenameColumn(
                name: "subject_es",
                schema: "app",
                table: "health_test_notification_template_versions",
                newName: "subject");

            migrationBuilder.RenameColumn(
                name: "name_es",
                schema: "app",
                table: "health_test_notification_template_versions",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "body_template_es",
                schema: "app",
                table: "health_test_notification_template_versions",
                newName: "body_template");
        }
    }
}
