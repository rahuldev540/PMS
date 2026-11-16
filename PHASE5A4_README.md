# Phase 5A4 — Settings controller CMS surface cleanup

Starting point: Phase 5A3.

## Changes
- Removed the redundant CMS-specific permission fallback in PharmacyAdminSettingsController GET; only the existing Settings View check remains. The fallback previously repeated the same Settings permission check.
- Removed obsolete CMS integration endpoint comments (no active endpoints were deleted).
- Removed legacy CMS explanatory comments in the pharmacy settings update path.
- Removed an explicit `HospitalId = null` initializer from the main-branch registration path. Entity/schema property is retained for compatibility until database cleanup.

## Scope / limitations
- This is an incremental controller cleanup, **not** complete removal of HospitalId or CMS from the project.
- No EF Core entities or mappings were changed; **no migration** is required for this phase.
- The .NET SDK is unavailable in the build environment, so this archive has NOT been compiled here. Run `dotnet restore .\Pharmacy.sln` and `dotnet build .\Pharmacy.sln` locally.
- Test Admin Settings GET and pharmacy registration in Swagger after building.
