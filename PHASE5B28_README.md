# Phase 5B28 — safe multi-file standalone cleanup + regression guard

Based on the **successful Phase 5B27 hotfix**, not the broken original 5B27 package.

## Changes
- Updated standalone auth/tenant documentation in seven C# files (comments only).
- Added scripts/Check-StandaloneRegression.ps1 to check active audit/revenue symbols and run dotnet restore/build, failing on nonzero exit codes.
- Kept all active endpoints, permissions, entities, schema, and business logic unchanged.
- No migration required.

## Test
From the extracted project root in PowerShell:

```powershell
.\scripts\Check-StandaloneRegression.ps1
dotnet run --project .\PharmacyOnly.Api.csproj
```

Verify Super Admin audit listing, summary, pharmacy-wise revenue, Admin login and Pharmacist login.

**Important:** This batch intentionally avoids risky functional removal. Legacy HospitalId database mappings remain and require a separate coordinated EF migration. The package integrity and source checks were performed; compilation must be verified on a machine with the .NET SDK.
