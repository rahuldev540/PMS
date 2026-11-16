$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet tool restore
dotnet restore ".\PharmacyOnly.Api.csproj"
dotnet clean ".\PharmacyOnly.Api.csproj"
dotnet build ".\PharmacyOnly.Api.csproj"

$migrationDirectory = Join-Path $PSScriptRoot "Migrations"
$hasMigration = Test-Path $migrationDirectory

if (-not $hasMigration) {
    dotnet tool run dotnet-ef migrations add InitialCreate `
        --project ".\PharmacyOnly.Api.csproj" `
        --startup-project ".\PharmacyOnly.Api.csproj" `
        --context "AuthDemo.Data.AppDbContext" `
        --output-dir "Migrations" `
        --verbose
}
else {
    Write-Host "A Migrations folder already exists; skipping InitialCreate."
}

dotnet tool run dotnet-ef database update `
    --project ".\PharmacyOnly.Api.csproj" `
    --startup-project ".\PharmacyOnly.Api.csproj" `
    --context "AuthDemo.Data.AppDbContext" `
    --verbose
