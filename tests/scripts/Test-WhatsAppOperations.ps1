# Validación de scripts operativos sin red ni cambios externos (credenciales ficticias de proceso).
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$metaPath = Join-Path $repo 'docs/modules/whatsapp/Meta-WhatsApp.ps1'
$meta = [ScriptBlock]::Create([IO.File]::ReadAllText($metaPath).Replace('#Requires -Version 7.0',''))
$script:requests = [Collections.Generic.List[object]]::new()
$script:recategorized = $false
function Invoke-RestMethod {
    param($Method, $Uri, $Headers, $ContentType, $Body)
    if ($Method -eq 'GET' -and $Uri -like '*message_templates*') {
        $data = @($script:requests | Where-Object { $_.Uri -like '*message_templates' } | ForEach-Object {
            [pscustomobject]@{ name=$_.Body.name; language=$_.Body.language; status='APPROVED'; category=$(if ($script:recategorized) {'MARKETING'} else {'UTILITY'}) }
        })
        return @{data=$data}
    }
    $parsed = if ($Body) { $Body | ConvertFrom-Json } else { $null }
    $script:requests.Add([pscustomobject]@{ Method=$Method; Uri=$Uri; Body=$parsed })
    return @{success=$true; messages=@(@{id='wamid.simulated'})}
}
function Assert-True($condition, [string]$message) { if (-not $condition) { throw $message } }
$original = $env:WHATSAPP_ACCESS_TOKEN
try {
    $env:WHATSAPP_ACCESS_TOKEN = 'test-only-not-a-credential'
    $null = & $meta -Action CreateTemplates -WabaId 'test-waba' -Apply
    Assert-True ($script:requests.Count -eq 8) 'Deben crearse exactamente ocho plantillas.'
    foreach ($request in $script:requests) {
        Assert-True ($request.Body.category -eq 'UTILITY') 'Categoría inesperada.'
        Assert-True ($request.Body.components[0].example.body_text.Count -eq 1) 'Ejemplo debe contener una fila.'
        Assert-True ($request.Body.components[0].example.body_text[0].Count -eq 2) 'Ejemplo debe contener nombre y detalle.'
    }
    $null = & $meta -Action VerifyTemplates -WabaId 'test-waba' -Apply
    $before = $script:requests.Count
    $null = & $meta -Action CreateTemplates -WabaId 'test-waba' -Apply
    Assert-True ($script:requests.Count -eq $before) 'La creación debe ser idempotente por nombre e idioma.'
    $null = & $meta -Action Plan
    Assert-True ($script:requests.Count -eq $before) 'Plan no debe hacer peticiones.'
    $script:recategorized = $true
    $rejected = $false
    try { $null = & $meta -Action VerifyTemplates -WabaId 'test-waba' -Apply } catch { $rejected = $true }
    Assert-True $rejected 'Una plantilla recategorizada debe bloquear la activación.'
    Write-Host 'OK: ocho payloads utility es/en, ejemplos, idempotencia, plan sin red y rechazo de marketing.'
}
finally { $env:WHATSAPP_ACCESS_TOKEN = $original }
