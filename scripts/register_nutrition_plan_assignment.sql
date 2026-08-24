-- Register the NutritionPlanAssignment migration in history
-- The table already exists, we just need EF to know it was applied
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260820210000_AddNutritionPlanAssignment', '10.0.10');

-- Verify all wellness-related migrations
SELECT "MigrationId" FROM "__EFMigrationsHistory" 
WHERE "MigrationId" LIKE '%Wellness%' OR "MigrationId" LIKE '%NutritionPlan%'
ORDER BY "MigrationId";
