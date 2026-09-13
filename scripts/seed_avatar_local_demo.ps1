param(
    [Parameter(Mandatory = $true)][ValidateSet('Seed', 'Remove')][string]$Mode,
    [Parameter(Mandatory = $true)][ValidateSet('Development')][string]$Environment,
    [string]$Psql = 'C:/Program Files/PostgreSQL/18/bin/psql.exe'
)
# Fixture local explícito; cuenta de pruebas confirmada por su propietario.
# No se ejecuta al arrancar la API. No contiene credenciales.
$ErrorActionPreference = 'Stop'
$config = Get-Content (Join-Path $PSScriptRoot '../src/CoppAddresd.Api/appsettings.json') -Raw | ConvertFrom-Json
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString($config.ConnectionStrings.DefaultConnection)
if ([string]$connection['Host'] -notin @('localhost', '127.0.0.1', '::1') -or [string]$connection['Database'] -ne 'coppaddresd') {
    throw 'Este fixture solo admite la BD coppaddresd en loopback local.'
}
$savedPassword = $env:PGPASSWORD
$savedTimeout = $env:PGCONNECT_TIMEOUT
$savedOptions = $env:PGOPTIONS
try {
    $env:PGPASSWORD = [string]$connection['Password']
    $env:PGCONNECT_TIMEOUT = '5'
    $env:PGOPTIONS = '-c statement_timeout=15000 -c lock_timeout=5000'
    $sql = @'
BEGIN;
SELECT pg_advisory_xact_lock(47640911);
DO $fixture$
DECLARE
  patient uuid := 'a90bd89b-984f-4a96-8095-9da2fb2bc3da';
  actor uuid := '8569f989-3d7b-4130-bc20-0c41c1e188ef';
  marker text := 'DATOS DE PRUEBA LOCAL - avatar-phase4-20260911';
BEGIN
  IF current_database() <> 'coppaddresd' OR inet_server_addr() NOT IN ('127.0.0.1'::inet, '::1'::inet) THEN
    RAISE EXCEPTION 'Destino no local';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM app.patient_profiles WHERE id=patient AND user_id=actor
      AND document_number='9000000001' AND email='paciente.antares@coppaddresd.com') THEN
    RAISE EXCEPTION 'La identidad de la cuenta de pruebas no coincide';
  END IF;
  IF '__MODE__' = 'Remove' THEN
    DELETE FROM app.clinical_measurements WHERE patient_id=patient AND created_by=actor AND notes=marker
      AND id IN ('47640911-0000-4000-8000-000000000001', '47640911-0000-4000-8000-000000000002', '47640911-0000-4000-8000-000000000003');
  ELSE
    IF EXISTS (SELECT 1 FROM app.clinical_measurements WHERE patient_id=patient AND notes IS DISTINCT FROM marker) THEN
      RAISE EXCEPTION 'La cuenta contiene otras mediciones; revisar antes de añadir el fixture';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM app.measurement_metrics m JOIN app.unit_of_measures u ON u.id=m.default_unit_id
        WHERE m.code='weight' AND m.is_active AND u.code='kg' AND u.is_active) THEN
      RAISE EXCEPTION 'Falta el catálogo activo weight/kg';
    END IF;
    INSERT INTO app.clinical_measurements (id,patient_id,metric_id,value,unit_id,observed_at,source,notes,created_by)
    SELECT v.id::uuid,patient,m.id,v.weight,m.default_unit_id,v.observed::timestamptz,'patient',marker,actor
    FROM (VALUES
      ('47640911-0000-4000-8000-000000000001',90.0,'2026-07-13 12:00:00-05'),
      ('47640911-0000-4000-8000-000000000002',94.0,'2026-08-12 12:00:00-05'),
      ('47640911-0000-4000-8000-000000000003',86.0,'2026-09-11 12:00:00-05')
    ) AS v(id,weight,observed)
    CROSS JOIN app.measurement_metrics m WHERE m.code='weight' AND m.is_active
    ON CONFLICT (id) DO NOTHING;
  END IF;
END $fixture$;
SELECT observed_at,value,notes FROM app.clinical_measurements
WHERE patient_id='a90bd89b-984f-4a96-8095-9da2fb2bc3da' ORDER BY observed_at;
COMMIT;
'@
    $sql = $sql.Replace('__MODE__', $Mode)
    $sql | & $Psql -X -w -h ([string]$connection['Host']) -p ([string]$connection['Port']) -U ([string]$connection['Username']) -d ([string]$connection['Database']) -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'El fixture falló; transacción revertida.' }
    Write-Output 'La API puede conservar el historial anterior hasta 5 minutos por su caché existente.'
} finally {
    $env:PGPASSWORD = $savedPassword
    $env:PGCONNECT_TIMEOUT = $savedTimeout
    $env:PGOPTIONS = $savedOptions
}
