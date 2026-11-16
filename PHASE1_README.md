# Phase 1 — Standalone authentication surface (incremental)

Source: PMS_TARGETED_PERMISSION_COMPILE_FIX(1).zip.

## Changes
- Unified login response reports Standalone only; no CMS/hospital identifiers returned.
- Admin assignment-status response reports Standalone only; no CMS/hospital identifiers returned.
- Pharmacist assignment-status response omits HospitalId.
- Login audit no longer copies user.HospitalId into ClinicId.
- Pharmacist login DTO hides legacy CMS/hospital fields from JSON while retaining them internally for compatibility with the existing service and database.
- Existing JWT already uses PharmacyId and BranchId, not HospitalId.

## Limitations
This is **Phase 1 only**, NOT a fully standalone PMS. Other controllers, services, database mappings and legacy models still reference HospitalId and CMS. Do not remove database columns yet. Build and integration tests are NOT verified here (dotnet SDK unavailable).

## Database migration
**Not needed for Phase 1.** No entity, schema, or EF migration was changed. Do not run Add-Migration or Update-Database for this phase.

## Steps
1. Back up the working project and SQL Server database.
2. Extract ZIP into a NEW empty folder; solution and .csproj are at ZIP root.
3. In Visual Studio 2022 Developer PowerShell from extracted folder: `dotnet restore .\Pharmacy.sln` then `dotnet build .\Pharmacy.sln`.
4. If build succeeds, `dotnet run --project .\PharmacyOnly.Api.csproj`.
5. Test Super Admin, Admin and Pharmacist logins. Confirm PharmacyId and BranchId, permissions and that login responses do not expose hospital/CMS fields.
6. Test Admin assignment status and Pharmacist assignment status. Check role/branch isolation and ensure the existing frontend is not relying on removed response properties.
7. If a build or runtime error occurs, retain the old working deployment and share the exact error before continuing.

## Next phases
Pharmacy/branch CMS cleanup, inventory, prescriptions/billing, and finally EF database migration after all dependencies are removed.
