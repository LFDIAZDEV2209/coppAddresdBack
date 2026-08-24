-- Remove the duplicate migration from history
-- AddWellnessModuleToSnapshot is a duplicate of AddWellnessModule + AddAgentSlug
DELETE FROM "__EFMigrationsHistory" 
WHERE "MigrationId" = '20260820172937_AddWellnessModuleToSnapshot';

-- Verify
SELECT "MigrationId" FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '%Wellness%';
