# Runs the ClaimGuard API locally without Docker or Kubernetes.
#
# Reads .env at the repo root, maps the friendly key names to the
# double-underscore config names ASP.NET Core expects, then starts the API
# on http://localhost:5000 (Swagger at /swagger).
#
#   powershell -ExecutionPolicy Bypass -File scripts/run-api-local.ps1

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

if (-not (Test-Path .env)) {
    Write-Error ".env not found at the repo root. Copy .env.example to .env and fill it in first."
}

# .env uses friendly names (same as k8s/secrets.example.yaml); ASP.NET Core
# configuration needs Section__Key names. Map every known alias.
$map = @{
    'SUPABASE_CONNECTION'        = 'ConnectionStrings__Supabase'
    'REDIS_CONNECTION'           = 'Redis__Connection'
    'KAFKA_BOOTSTRAP'            = 'Kafka__BootstrapServers'
    'KAFKA_BOOTSTRAP_SERVERS'    = 'Kafka__BootstrapServers'
    'KAFKA_USER'                 = 'Kafka__SaslUsername'
    'KAFKA_SASL_USERNAME'        = 'Kafka__SaslUsername'
    'KAFKA_PASS'                 = 'Kafka__SaslPassword'
    'KAFKA_SASL_PASSWORD'        = 'Kafka__SaslPassword'
    'KAFKA_SASL_MECHANISM'       = 'Kafka__SaslMechanism'
    'RABBITMQ_URL'               = 'RabbitMq__Url'
    'GEMINI_API_KEY'             = 'Gemini__ApiKey'
    'JWT_SECRET'                 = 'Jwt__SigningSecret'
    'SUPABASE_URL'               = 'Supabase__Url'
    'SUPABASE_SERVICE_KEY'       = 'Supabase__ServiceRoleKey'
    'SUPABASE_SERVICE_ROLE_KEY'  = 'Supabase__ServiceRoleKey'
    'SEED_BOOTSTRAP_SECRET'      = 'Seed__BootstrapSecret'
}

Get-Content .env | ForEach-Object {
    if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=(.*)$') {
        $key = $Matches[1]
        $val = $Matches[2].Trim().Trim('"')
        if ($map.ContainsKey($key)) {
            Set-Item -Path "Env:$($map[$key])" -Value $val
        }
    }
}

# Fail fast (10s) when the database is unreachable instead of letting
# requests hang for a minute with no visible error.
if ($env:ConnectionStrings__Supabase -and $env:ConnectionStrings__Supabase -notmatch 'Timeout=') {
    $env:ConnectionStrings__Supabase += ';Timeout=10'
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5000'

Write-Host 'Starting ClaimGuard API on http://localhost:5000 ...'
dotnet run --project backend/src/ClaimGuard.Api
