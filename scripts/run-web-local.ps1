# Runs the Next.js web console locally without Docker or Kubernetes.
#
# Builds .env.local from the repo-root .env (Next.js loads .env.local
# automatically), then starts the dev server on http://localhost:3000.
#
#   powershell -ExecutionPolicy Bypass -File scripts/run-web-local.ps1

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

if (-not (Test-Path .env)) {
    Write-Error ".env not found at the repo root. Copy .env.example to .env and fill it in first."
}

$lines = @('NEXT_PUBLIC_API_BASE_URL=http://localhost:5000')

# Carry over every NEXT_PUBLIC_* key the user already set.
Get-Content .env | ForEach-Object {
    if ($_ -match '^\s*(NEXT_PUBLIC_[A-Za-z0-9_]*)\s*=(.*)$') {
        $lines += "$($Matches[1])=$($Matches[2].Trim().Trim('"'))"
    }
}

# Map the Supabase keys if they are stored under the non-NEXT_PUBLIC names.
$hasSupabaseUrl = ($lines | Where-Object { $_ -like 'NEXT_PUBLIC_SUPABASE_URL=*' }).Count -gt 0
$hasAnonKey     = ($lines | Where-Object { $_ -like 'NEXT_PUBLIC_SUPABASE_ANON_KEY=*' }).Count -gt 0

Get-Content .env | ForEach-Object {
    if ($_ -match '^\s*SUPABASE_URL\s*=(.*)$' -and -not $hasSupabaseUrl) {
        $lines += "NEXT_PUBLIC_SUPABASE_URL=$($Matches[1].Trim().Trim('"'))"
    }
    if ($_ -match '^\s*SUPABASE_ANON_KEY\s*=(.*)$' -and -not $hasAnonKey) {
        $lines += "NEXT_PUBLIC_SUPABASE_ANON_KEY=$($Matches[1].Trim().Trim('"'))"
    }
}

$lines | Set-Content .env.local
Write-Host 'Wrote .env.local with keys:'
$lines | ForEach-Object { ($_ -split '=', 2)[0] }

pnpm install
pnpm dev
