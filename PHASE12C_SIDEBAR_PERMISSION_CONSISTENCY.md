# Phase 12C – Sidebar permission consistency

## Changes
- Controllers/UnifiedAuthController.cs: for PharmacyAdmin and Pharmacist, login `user.sidebarModules` now includes only modules with `canView: true` in the effective `user.permissions` response.
- Super Admin sidebar remains unchanged.
- Missing permission records do not grant sidebar visibility.
- No new API, no database migration, no changes to internal IDs, JWT claims, authorization checks, or permission defaults at account creation.

## Verify
1. `dotnet restore .\Pharmacy.sln`
2. `dotnet build .\Pharmacy.sln`
3. `dotnet run --project .\PharmacyOnly.Api.csproj`
4. Disable Purchase Orders and Stock Transfers for an Admin via existing permission API.
5. Login via `POST /api/auth/login`: check `user.permissions` has `canView:false` and `user.sidebarModules` omits both modules.
6. Check the relevant restricted API still returns 403. Re-enable permission and login again to check visibility.
7. Repeat with a Pharmacist.

## Verification limits
Source-level replacement and ZIP integrity checked. No .NET SDK available here; compilation and runtime/database integration tests must be performed locally.
