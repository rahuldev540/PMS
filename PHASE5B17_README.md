# Phase 5B17 — Remove legacy hospital scope from Super Admin status audits

Base: Phase 5B16.

## Changes
- In `Controllers/PharmacySuperAdminController.cs`, changed audit calls for medicine status, pharmacy-admin update, pharmacy-admin deactivation, and pharmacy-admin status to pass `null` instead of `medicine.HospitalId` / `admin.HospitalId`.
- Kept branch IDs, status mutations, and database entities unchanged.
- No schema migration required.

## Limitations
- This is a narrow cleanup, not complete HospitalId removal. HospitalId remains in other active services, entities, and migrations.
- Audit records created by these four operations no longer contain hospital identifiers.
- ZIP integrity checked, but no .NET SDK build was performed here.

## Run
```powershell
dotnet restore .\Pharmacy.sln
dotnet build .\Pharmacy.sln
dotnet run --project .\PharmacyOnly.Api.csproj
```
Test Super Admin admin status and medicine status flows and their audit records.
