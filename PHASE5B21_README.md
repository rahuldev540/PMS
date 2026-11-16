# Phase 5B21 — Notification recipient hospital lookup cleanup

Base: Phase 5B20.

Updated `Controllers/PharmacySuperAdminController.cs` in Super Admin notification creation:
- Branch recipient lookup selects branch Id only, not HospitalId.
- User recipient lookup selects Id, BranchId, and IsActive only, not HospitalId.
- Notification creation explicitly sets the existing nullable `HospitalId` column to null.
- All, Branch, and User recipient types remain supported.
- Other HospitalId dependencies are **not** removed. No historical migrations changed.

**Migration:** None; schema is unchanged. Do not drop columns yet.

**Local verification:** `dotnet restore .\Pharmacy.sln`, `dotnet build .\Pharmacy.sln`, `dotnet run --project .\PharmacyOnly.Api.csproj`. Test creating notifications for All, Branch, and User and reading them back. The frontend must not offer Hospital recipient type. ZIP integrity was checked, but .NET build could not be run in the packaging environment.
