param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$controller = Join-Path $root 'Controllers/PharmacySuperAdminController.cs'
if (-not (Test-Path -LiteralPath $controller)) { throw "Controller not found: $controller" }
$content = Get-Content -LiteralPath $controller -Raw
$requiredPatterns = @{
    'Audit query helper declaration' = 'private\s+IQueryable<AuditLog>\s+BuildAuditLogQuery\s*\('
    'Audit validation helper declaration' = 'private\s+async\s+Task<IActionResult\?>\s+ValidateAuditFilters\s*\('
    'Active pharmacy revenue route' = '\[HttpGet\("reports/revenue"\)\]'
}
foreach ($name in $requiredPatterns.Keys) {
    $matches = [regex]::Matches($content, $requiredPatterns[$name])
    if ($matches.Count -ne 1) { throw "$name: expected one declaration, found $($matches.Count)" }
}
$requiredFiles = @('Pharmacy.sln','PharmacyOnly.Api.csproj','Controllers/PharmacySuperAdminController.cs')
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $file))) { throw "Required project file missing: $file" }
}
Write-Host 'Source regression checks passed.' -ForegroundColor Green
if (-not $SkipBuild) {
    Push-Location $root
    try {
        dotnet restore .\Pharmacy.sln
        if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed' }
        dotnet build .\Pharmacy.sln --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
    } finally { Pop-Location }
}
