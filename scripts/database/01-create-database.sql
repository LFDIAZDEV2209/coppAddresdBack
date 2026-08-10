-- ============================================================================
-- 01-create-database.sql
-- Crea la base de datos `coppaddresd` (UTF8) y el usuario de aplicación
-- `app_user` para desarrollo local. Re-ejecutable (guarda de existencia).
--
-- Nota de nombres: PostgreSQL pliega identificadores sin comillas a minúsculas,
-- por lo que `CREATE DATABASE coppAddresd` resulta en `coppaddresd`. El nombre
-- canónico del proyecto es `coppaddresd` (en minúsculas) en connection strings.
--
-- Ejecutar como superusuario postgres:
--   & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost `
--     -v app_password="TU_PASSWORD_DEV" -f scripts/database/01-create-database.sql
--
-- La password solo existe localmente: appsettings.json (gitignored) y
-- variables de entorno. NUNCA versionar credenciales.
-- Colación: se usa la del servidor (en producción AWS RDS: UTF8 + ICU).
-- ============================================================================

\set ON_ERROR_STOP on

-- 1. Base de datos (solo si no existe)
SELECT 'CREATE DATABASE coppaddresd WITH ENCODING ''UTF8'' TEMPLATE template0'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'coppaddresd')\gexec

-- 2. Usuario de aplicación (solo si no existe)
SELECT 'CREATE ROLE app_user LOGIN PASSWORD ' || quote_literal(:'app_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'app_user')\gexec

-- 3. Permisos de base
GRANT CONNECT ON DATABASE coppaddresd TO app_user;
ALTER DATABASE coppaddresd OWNER TO app_user;

-- 4. Permisos en schema public (tablas de negocio)
\c coppaddresd
GRANT ALL ON SCHEMA public TO app_user;

-- Nota: el schema `audit` y su GRANT los crea la migración EF
-- `InitialAuditSchema` (dotnet ef database update).
