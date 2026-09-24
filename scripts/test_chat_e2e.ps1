#Requires -Version 5.1
<#
.SYNOPSIS
    Verificacion E2E Fase 9 (Chat IA): Gateway -> Api -> AI Service.
    Prueba login, lectura de thread propio, blindaje anti-IDOR y feedback.
.DESCRIPTION
    Pasos (Tarea 4.1 de OpenSpec app-fase-9-chat-ia-agentes):
      a) Login en Gateway (POST /api/auth/login) y extraccion del accessToken.
      b) Lectura de thread propio (GET /api/v1/threads/{id}/messages) -> 200.
      c) Intento de IDOR (?userId=otro_usuario): el backend debe ignorar el
         query param y usar el JWT; sin token debe responder 401.
      d) Feedback (POST /api/v1/chat/feedback) -> 200, o 502/503 elegante si
         el ai-service no esta arriba (nunca 500).
    Sale con codigo 0 si todo pasa, 1 si algo falla.
.EXAMPLE
    .\scripts\test_chat_e2e.ps1
.EXAMPLE
    .\scripts\test_chat_e2e.ps1 -DocumentNumber "55551234" -Password "Demo1234!"
#>
[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://localhost:5080",
    [string]$DocumentNumber = "55551234",
    [string]$Password = "Demo1234!",
    [string]$Application = "app",
    [string]$ThreadId = "user_55551234_main",
    [int]$TimeoutSec = 30
)

$ErrorActionPreference = "Stop"

$script:Passed = 0
$script:Failed = 0

function Write-Step([string]$Name) {
    Write-Host ""
    Write-Host "=== $Name ===" -ForegroundColor Cyan
}

function Write-Pass([string]$Name) {
    $script:Passed++
    Write-Host "PASS: $Name" -ForegroundColor Green
}

function Write-Fail([string]$Name, [string]$Detail = "") {
    $script:Failed++
    Write-Host "FAIL: $Name" -ForegroundColor Red
    if ($Detail) { Write-Host "      $Detail" -ForegroundColor Red }
}

function Invoke-E2E {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Url,
        [string]$Token = "",
        [object]$Body = $null
    )
    $headers = @{ Accept = "application/json" }
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    $params = @{
        Uri             = $Url
        Method          = $Method
        Headers         = $headers
        TimeoutSec      = $TimeoutSec
        UseBasicParsing = $true
        ErrorAction     = "Stop"
    }
    if ($null -ne $Body) {
        $params["ContentType"] = "application/json"
        $params["Body"] = ($Body | ConvertTo-Json -Depth 5 -Compress)
    }
    try {
        $resp = Invoke-WebRequest @params
        return @{ StatusCode = [int]$resp.StatusCode; Body = $resp.Content }
    }
    catch {
        $webEx = $_.Exception
        if ($webEx.Response) {
            $status = [int]$webEx.Response.StatusCode
            $content = ""
            try {
                $stream = $webEx.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $content = $reader.ReadToEnd()
                $reader.Close()
            }
            catch { }
            return @{ StatusCode = $status; Body = $content }
        }
        throw
    }
}

# --- a) Login en Gateway ---
Write-Step "a) Login en Gateway"
$loginBody = @{
    documentNumber = $DocumentNumber
    password       = $Password
    application    = $Application
}
$login = Invoke-E2E -Method "POST" -Url "$GatewayBaseUrl/api/auth/login" -Body $loginBody
if ($login.StatusCode -ne 200) {
    Write-Fail "login responde 200" "HTTP $($login.StatusCode): $($login.Body)"
    Write-Host ""
    Write-Host "E2E ROJO: sin token no se puede continuar." -ForegroundColor Red
    exit 1
}
$accessToken = ""
try { $accessToken = ($login.Body | ConvertFrom-Json).accessToken } catch { }
if ([string]::IsNullOrWhiteSpace($accessToken)) {
    Write-Fail "login devuelve accessToken" "Body: $($login.Body)"
    exit 1
}
Write-Pass "login responde 200 y entrega accessToken"

# --- b) Lectura de thread propio ---
Write-Step "b) Lectura de thread propio"
$threadUrl = "$GatewayBaseUrl/api/v1/threads/$ThreadId/messages"
$own = Invoke-E2E -Method "GET" -Url $threadUrl -Token $accessToken
if ($own.StatusCode -ne 200) {
    Write-Fail "GET thread propio responde 200" "HTTP $($own.StatusCode): $($own.Body)"
}
else {
    Write-Pass "GET thread propio responde 200"
}
$ownJson = $null
try { $ownJson = $own.Body | ConvertFrom-Json } catch { }
if ($null -eq $ownJson -or $ownJson.threadId -ne $ThreadId) {
    Write-Fail "respuesta del thread trae threadId=$ThreadId" "Body: $($own.Body)"
}
else {
    Write-Pass "respuesta del thread trae threadId y messageCount=$($ownJson.messageCount)"
}

# --- c) Blindaje anti-IDOR ---
Write-Step "c) Blindaje anti-IDOR"
# c.1) Sin token -> 401 (el [Authorize] cierra la puerta).
$anon = Invoke-E2E -Method "GET" -Url $threadUrl
if ($anon.StatusCode -eq 401) {
    Write-Pass "GET thread sin token responde 401"
}
else {
    Write-Fail "GET thread sin token responde 401" "HTTP $($anon.StatusCode): $($anon.Body)"
}
# c.2) Con token + ?userId=otro_usuario -> el backend ignora el query param:
# la respuesta debe ser identica a la del thread propio (mismo threadId y
# mismo messageCount: el alcance salio del JWT, no del query).
$spoof = Invoke-E2E -Method "GET" -Url "$threadUrl`?userId=otro_usuario" -Token $accessToken
if ($spoof.StatusCode -ne 200) {
    Write-Fail "GET thread con userId ajeno responde 200" "HTTP $($spoof.StatusCode): $($spoof.Body)"
}
else {
    $spoofJson = $null
    try { $spoofJson = $spoof.Body | ConvertFrom-Json } catch { }
    if ($null -ne $spoofJson -and $spoofJson.threadId -eq $ThreadId -and $spoofJson.messageCount -eq $ownJson.messageCount) {
        Write-Pass "query param userId ignorado: misma respuesta que con JWT (threadId y messageCount iguales)"
    }
    else {
        Write-Fail "query param userId ignorado" "Esperado threadId=$ThreadId messageCount=$($ownJson.messageCount). Body: $($spoof.Body)"
    }
}

# --- d) Feedback ---
Write-Step "d) Endpoint de feedback"
$feedbackBody = @{
    executionId = "e2e_probe_exec_1"
    threadId    = $ThreadId
    rating      = 5
    comment     = "Respuesta excelente"
}
$feedback = Invoke-E2E -Method "POST" -Url "$GatewayBaseUrl/api/v1/chat/feedback" -Token $accessToken -Body $feedbackBody
if ($feedback.StatusCode -eq 200) {
    Write-Pass "POST feedback responde 200 (ai-service disponible)"
}
elseif ($feedback.StatusCode -eq 502 -or $feedback.StatusCode -eq 503) {
    # Degradacion elegante: ProblemDetails con mensaje clinico, nunca 500.
    if ($feedback.Body -match "asistente inteligente no est") {
        Write-Pass "POST feedback responde $($feedback.StatusCode) elegante con mensaje clinico (ai-service no disponible)"
    }
    else {
        Write-Fail "POST feedback degradado sin mensaje clinico" "HTTP $($feedback.StatusCode): $($feedback.Body)"
    }
}
else {
    Write-Fail "POST feedback responde 200 (o 502/503 elegante)" "HTTP $($feedback.StatusCode): $($feedback.Body)"
}

# --- Resumen ---
Write-Host ""
Write-Host "E2E Fase 9: $($script:Passed) PASS, $($script:Failed) FAIL." -ForegroundColor $(if ($script:Failed -eq 0) { "Green" } else { "Red" })
if ($script:Failed -gt 0) { exit 1 }
Write-Host "E2E VERDE: Gateway -> Api -> AI Service verificados." -ForegroundColor Green
