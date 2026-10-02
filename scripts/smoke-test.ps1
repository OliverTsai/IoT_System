[CmdletBinding()]
param(
    [string]$EnvFile = ".env",
    [int]$TelemetryTimeoutSeconds = 45
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $EnvFile -PathType Leaf)) {
    throw "Environment file '$EnvFile' does not exist."
}

$settings = @{}
Get-Content -LiteralPath $EnvFile | ForEach-Object {
    if ($_ -match "^\s*([^#][^=]*)=(.*)$") {
        $settings[$matches[1].Trim()] = $matches[2].Trim()
    }
}

function Get-ConfigurationValue {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [string]$DefaultValue = ""
    )

    $environmentValue = [Environment]::GetEnvironmentVariable($Name)
    if (-not [string]::IsNullOrWhiteSpace($environmentValue)) {
        return $environmentValue
    }

    if ($settings.ContainsKey($Name) -and
        -not [string]::IsNullOrWhiteSpace($settings[$Name])) {
        return $settings[$Name]
    }

    return $DefaultValue
}

foreach ($requiredKey in @(
    "BOOTSTRAP_ADMIN_USERNAME",
    "BOOTSTRAP_ADMIN_PASSWORD"
)) {
    if ([string]::IsNullOrWhiteSpace((Get-ConfigurationValue -Name $requiredKey))) {
        throw "Define $requiredKey in the process environment or '$EnvFile'."
    }
}
$username = Get-ConfigurationValue -Name "BOOTSTRAP_ADMIN_USERNAME"
$password = Get-ConfigurationValue -Name "BOOTSTRAP_ADMIN_PASSWORD"
$httpsPort = Get-ConfigurationValue -Name "WEB_HTTPS_PORT" -DefaultValue "8443"
$baseUrl = "https://localhost:$httpsPort"
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$commonRequest = @{
    SkipCertificateCheck = $true
}

Write-Host "Checking container and database health..."
$live = Invoke-RestMethod @commonRequest -Uri "$baseUrl/health/live"
$ready = Invoke-RestMethod @commonRequest -Uri "$baseUrl/health/ready"
if ($live.status -ne "Healthy" -or $ready.status -ne "Healthy") {
    throw "Health checks did not report Healthy."
}
$web = Invoke-WebRequest @commonRequest -Uri $baseUrl
if ($web.Content -notmatch '<div id="app"></div>' -or
    -not $web.Headers.ContainsKey("Content-Security-Policy")) {
    throw "The Vue entry point or its Content-Security-Policy header is missing."
}
$openApi = Invoke-RestMethod @commonRequest -Uri "$baseUrl/openapi/v1.json"
if ($null -eq $openApi.paths.'/api/devices' -or
    $null -eq $openApi.paths.'/api/alerts') {
    throw "The container OpenAPI document is missing required API paths."
}

Write-Host "Logging in through the HTTPS reverse proxy..."
$csrf = Invoke-RestMethod @commonRequest `
    -Uri "$baseUrl/api/auth/csrf" `
    -WebSession $session
$loginBody = @{
    username = $username
    password = $password
} | ConvertTo-Json
$null = Invoke-RestMethod @commonRequest `
    -Uri "$baseUrl/api/auth/login" `
    -Method Post `
    -ContentType "application/json" `
    -Headers @{ "X-CSRF-TOKEN" = $csrf.token } `
    -Body $loginBody `
    -WebSession $session

Write-Host "Checking protected REST and SignalR endpoints..."
$devices = Invoke-RestMethod @commonRequest `
    -Uri "$baseUrl/api/devices?page=1&pageSize=100" `
    -WebSession $session
if ($devices.totalCount -lt 1) {
    throw "No devices were returned; demo data was not seeded."
}
$null = Invoke-RestMethod @commonRequest `
    -Uri "$baseUrl/api/alerts?page=1&pageSize=1" `
    -WebSession $session
$null = Invoke-RestMethod @commonRequest `
    -Uri "$baseUrl/hubs/monitoring/negotiate?negotiateVersion=1" `
    -Method Post `
    -ContentType "application/json" `
    -Body "{}" `
    -WebSession $session

Write-Host "Waiting for simulator telemetry to traverse MQTT and PostgreSQL..."
$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TelemetryTimeoutSeconds)
$telemetryObserved = $false
do {
    foreach ($device in $devices.items) {
        $details = Invoke-RestMethod @commonRequest `
            -Uri "$baseUrl/api/devices/$($device.id)" `
            -WebSession $session
        if ($null -ne $details.latestTelemetry) {
            $telemetryObserved = $true
            break
        }
    }

    if (-not $telemetryObserved) {
        Start-Sleep -Seconds 2
    }
} while (-not $telemetryObserved -and [DateTimeOffset]::UtcNow -lt $deadline)

if (-not $telemetryObserved) {
    throw "No simulator telemetry was visible before the timeout."
}

$csrf = Invoke-RestMethod @commonRequest `
    -Uri "$baseUrl/api/auth/csrf" `
    -WebSession $session
$null = Invoke-RestMethod @commonRequest `
    -Uri "$baseUrl/api/auth/logout" `
    -Method Post `
    -Headers @{ "X-CSRF-TOKEN" = $csrf.token } `
    -WebSession $session

Write-Host "Smoke test passed: HTTPS, authentication, REST, SignalR negotiation, MQTT, and PostgreSQL are healthy."
