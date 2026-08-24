-- 1. Check if nutrition_plan_assignments exists
SELECT EXISTS (
    SELECT FROM information_schema.tables 
    WHERE table_schema = 'app' 
    AND table_name = 'nutrition_plan_assignments'
) AS table_exists;

-- 2. Check migration history for AddNutritionPlanAssignment
SELECT "MigrationId" 
FROM "__EFMigrationsHistory" 
WHERE "MigrationId" LIKE '%NutritionPlanAssignment%';

-- 3. Check all tables in app schema
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'app' 
ORDER BY table_name;
