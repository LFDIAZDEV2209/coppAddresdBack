START TRANSACTION;
ALTER TABLE auth."UserPreferences" ADD "AvatarConfiguration" jsonb;

INSERT INTO auth.__ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260914152543_AddAvatarConfigurationPreference', '10.0.10');

COMMIT;

