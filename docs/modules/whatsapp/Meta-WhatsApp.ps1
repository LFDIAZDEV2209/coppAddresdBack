#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Plan', 'CreateTemplates', 'ListTemplates', 'VerifyTemplates', 'RegisterWebhook', 'Subscribe', 'ProbeToken', 'SendTemplate')]
    [string]$Action = 'Plan',
    [string]$WabaId,
    [string]$CallbackUrl,
    [string]$Recipient,
    [string]$TemplateName = 'checkin_bienestar',
    [ValidateSet('es', 'en')][string]$Language = 'es',
    [string]$PatientName = 'Paciente QA',
    [string]$Detail = 'Tu actualización del programa está disponible en la app ANTARES.',
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
$VerbosePreference = 'SilentlyContinue'
$DebugPreference = 'SilentlyContinue'
$baseUrl = 'https://graph.facebook.com/v23.0'

# Solo estas ocho plantillas forman parte del canal. No hay plantillas de citas.
$templates = @(
    @{ name='checkin_bienestar'; language='es'; body='Hola {{1}}, tu check-in de bienestar del programa ANTARES está disponible: {{2}} Revisa tu programa en la app.' },
    @{ name='wellness_checkin'; language='en'; body='Hello {{1}}, your ANTARES program wellness check-in is available: {{2}} Review your program in the app.' },
    @{ name='racha_programa'; language='es'; body='Hola {{1}}, actualización de tu cuenta del programa ANTARES: tu racha actual es {{2}}. Si aún no registraste la actividad de hoy, hazlo en la app.' },
    @{ name='program_streak'; language='en'; body='Hello {{1}}, an update on your ANTARES account: your current streak is {{2}}. If you have not logged today''s activity yet, please do so in the app.' },
    @{ name='recordatorio_podcast'; language='es'; body='Hola {{1}}, actualización del podcast asignado en tu programa ANTARES: {{2}} Revisa la lección en la app.' },
    @{ name='podcast_reminder'; language='en'; body='Hello {{1}}, an update on the podcast assigned in your ANTARES program: {{2}} Review the lesson in the app.' },
    @{ name='recordatorio_evaluacion'; language='es'; body='Hola {{1}}, actualización de tu evaluación inicial del programa ANTARES: {{2}} Revisa tu evaluación en la app.' },
    @{ name='assessment_reminder'; language='en'; body='Hello {{1}}, an update on your initial ANTARES program assessment: {{2}} Review your assessment in the app.' }
)

if ($Action -eq 'Plan' -or -not $Apply) {
    $templates | ForEach-Object { [pscustomobject]@{ Name=$_.name; Language=$_.language; Category='UTILITY'; Body=$_.body } }
    Write-Host 'Plan local: no se hicieron peticiones. Para ejecutar una acción revisada, indicar -Apply.'
    return
}
function Require-Env([string]$name) {
    $value = [Environment]::GetEnvironmentVariable($name)
    if ([string]::IsNullOrWhiteSpace($value)) { throw "Falta la variable $name." }
    return $value
}
function Invoke-Meta([string]$method, [string]$path, [object]$body = $null, [string]$token = '') {
    if (-not $token) { $token = Require-Env 'WHATSAPP_ACCESS_TOKEN' }
    $parameters = @{ Method=$method; Uri="$baseUrl/$path"; Headers=@{Authorization="Bearer $token"} }
    if ($null -ne $body) { $parameters.ContentType='application/json'; $parameters.Body=($body | ConvertTo-Json -Depth 15 -Compress) }
    try { return Invoke-RestMethod @parameters }
    catch { throw 'Meta rechazó la operación. Revisar permisos, categoría y configuración en Meta; no se imprime cuerpo ni URL con credenciales.' }
}
function Get-Templates {
    if (-not $WabaId) { throw 'Indicar -WabaId.' }
    $all = @()
    $path = "$WabaId/message_templates?fields=name,language,status,category&limit=100"
    do {
        $page = Invoke-Meta 'GET' $path
        $all += @($page.data)
        # Construir paginación sin seguir URLs que pudieran contener un access_token.
        $after = $page.paging.cursors.after
        $more = $page.paging.next
        if ($more -and $after) { $path = "$WabaId/message_templates?fields=name,language,status,category&limit=100&after=$([Uri]::EscapeDataString($after))" }
    } while ($more -and $after)
    return $all
}
switch ($Action) {
    'CreateTemplates' {
        $existing = @(Get-Templates)
        foreach ($template in $templates) {
            if ($existing | Where-Object { $_.name -eq $template.name -and $_.language -eq $template.language }) { continue }
            $example = if ($template.language -eq 'en') { 'Your assigned program activity is pending in the app.' } else { 'Tu actividad asignada del programa sigue pendiente en la app.' }
            $body = @{ name=$template.name; language=$template.language; category='UTILITY'; components=@(
                @{ type='BODY'; text=$template.body; example=@{ body_text=@(,@('Paciente QA', $example)) } }
            ) }
            $null = Invoke-Meta 'POST' "$WabaId/message_templates" $body
            Write-Host "Enviada a revisión: $($template.name) ($($template.language))."
        }
    }
    'ListTemplates' { Get-Templates | Select-Object name,language,status,category }
    'VerifyTemplates' {
        $existing = @(Get-Templates)
        foreach ($template in $templates) {
            if (-not ($existing | Where-Object { $_.name -eq $template.name -and $_.language -eq $template.language -and $_.status -eq 'APPROVED' -and $_.category -eq 'UTILITY' })) {
                throw "Plantilla pendiente, rechazada o recategorizada: $($template.name). No activar recordatorios."
            }
        }
        Write-Host 'Las ocho plantillas están APPROVED y UTILITY.'
    }
    'RegisterWebhook' {
        if (-not $CallbackUrl.StartsWith('https://')) { throw 'Indicar -CallbackUrl HTTPS pública.' }
        $appId = Require-Env 'WHATSAPP_APP_ID'
        $appToken = "$appId|$(Require-Env 'WHATSAPP_APP_SECRET')"
        $null = Invoke-Meta 'POST' "$appId/subscriptions" @{ object='whatsapp_business_account'; callback_url=$CallbackUrl;
            verify_token=(Require-Env 'WHATSAPP_WEBHOOK_VERIFY_TOKEN'); fields='messages'; include_values=$true } $appToken
        Write-Host 'Registro solicitado; verificar el handshake en Meta.'
    }
    'Subscribe' {
        if (-not $WabaId) { throw 'Indicar -WabaId.' }
        $null = Invoke-Meta 'POST' "$WabaId/subscribed_apps" @{}
        Write-Host 'App suscrita a la WABA.'
    }
    'ProbeToken' {
        $appId = Require-Env 'WHATSAPP_APP_ID'
        $appToken = "$appId|$(Require-Env 'WHATSAPP_APP_SECRET')"
        $inputToken = [Uri]::EscapeDataString((Require-Env 'WHATSAPP_ACCESS_TOKEN'))
        $result = Invoke-Meta 'GET' "debug_token?input_token=$inputToken" $null $appToken
        if (-not $result.data.is_valid -or $result.data.app_id -ne $appId) { throw 'Token no válido para esta app.' }
        Write-Host 'Token válido para la app configurada.'
    }
    'SendTemplate' {
        if ($Recipient -notmatch '^\+?[1-9][0-9]{7,14}$') { throw 'Indicar -Recipient E.164 del destinatario QA autorizado.' }
        if (-not ($templates | Where-Object { $_.name -eq $TemplateName -and $_.language -eq $Language })) { throw 'Plantilla fuera del catálogo del canal.' }
        $approved = Get-Templates | Where-Object { $_.name -eq $TemplateName -and $_.language -eq $Language -and $_.status -eq 'APPROVED' -and $_.category -eq 'UTILITY' }
        if (-not $approved) { throw 'La plantilla debe estar APPROVED y UTILITY antes de enviar.' }
        $phone = Require-Env 'WHATSAPP_PHONE_NUMBER_ID'
        $null = Invoke-Meta 'POST' "$phone/messages" @{ messaging_product='whatsapp'; to=$Recipient.TrimStart('+'); type='template';
            template=@{name=$TemplateName; language=@{code=$Language}; components=@(@{type='body'; parameters=@(@{type='text';text=$PatientName}, @{type='text';text=$Detail})})} }
        Write-Host 'Plantilla QA aceptada por Meta; comprobar recepción en el dispositivo.'
    }
}
