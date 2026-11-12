#Requires -Version 7.0
[CmdletBinding()]
param([string]$SecretId = 'cooppadresd/whatsapp', [string]$Region = 'us-east-2', [switch]$Apply)
$ErrorActionPreference = 'Stop'
$VerbosePreference = 'SilentlyContinue'
$DebugPreference = 'SilentlyContinue'
if (-not $Apply) { Write-Host 'Plan local: se leerían los cinco campos con Read-Host seguro y se actualizaría Secrets Manager en memoria. No ejecutado.'; return }
Import-Module AWS.Tools.SecretsManager -ErrorAction Stop
function Read-SensitiveValue([string]$name) {
    $secure = Read-Host $name -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer); $secure.Dispose() }
}
$values = @{}
try {
    foreach ($key in @('WHATSAPP_ACCESS_TOKEN','WHATSAPP_PHONE_NUMBER_ID','WHATSAPP_APP_ID','WHATSAPP_APP_SECRET','WHATSAPP_WEBHOOK_VERIFY_TOKEN')) {
        $values[$key] = Read-SensitiveValue $key
        if ([string]::IsNullOrWhiteSpace($values[$key])) { throw 'No se admiten campos vacíos.' }
    }
    # SDK en el proceso: nunca credenciales en argumentos de CLI ni archivos temporales.
    $null = Write-SECSecretValue -SecretId $SecretId -SecretString ($values | ConvertTo-Json -Compress) -Region $Region
    Write-Host 'Nueva versión guardada. Falta desplegar y validar antes de revocar las credenciales anteriores.'
}
catch { throw 'No se completó la actualización del secret; revisar permisos y configuración sin imprimir valores.' }
finally { $values.Clear() }
