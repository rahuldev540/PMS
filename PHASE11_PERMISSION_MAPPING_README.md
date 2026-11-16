# Phase 11 – Pharmacist Permission User ID Mapping Fix

## Scope
- Fixed `Controllers/PharmacistAdminController.cs` in the `PUT /api/pharmacy-admin/pharmacists/{id}/permissions` action.
- Newly inserted `UserPermission` rows now use `internalPharmacistId.Value` (the global `Users.Id` foreign key) instead of `id` (the pharmacy-scoped public Pharmacist ID).
- Existing permission records already looked up using the internal User ID; that behavior is unchanged.
- The single `Pharmacist` role, all-TRUE creation defaults, branch assignment, and Admin-controlled edits are unchanged.
- No new API endpoints, request DTO changes, or EF migrations.

## Install (Windows PowerShell)
1. Back up your current project and database. Stop the running API.
2. Extract the ZIP to a NEW folder. The solution and project files are at ZIP root (no extra project wrapper folder).
3. Open PowerShell in the extracted folder and run:
   `dotnet restore .\Pharmacy.sln`
   `dotnet build .\Pharmacy.sln`
   `dotnet run --project .\PharmacyOnly.Api.csproj`
4. Open Swagger at the address shown by `dotnet run`.
5. Log in as Pharmacy Admin and test:
   - `GET /api/pharmacy-admin/pharmacists/{id}/permissions`
   - `PUT /api/pharmacy-admin/pharmacists/{id}/permissions`
   - Repeat GET to confirm saved values.
6. Use a public Pharmacist ID in `{id}`; do NOT send an internal Users.Id from the frontend.

## Important test for the corrected path
The fix affects *insertion of missing permission records*. Updating an existing permission row already used the correct internal User ID. To test the insertion path, use a safe test database or a test Pharmacist with a missing permission row. Do not delete production permission records merely to test.

## Known limitations (not changed in this targeted release)
- The Pharmacist inventory controller currently grants View to an assigned active Pharmacist without checking the `Stock` UserPermissions flag. This should be addressed in a separate permission-enforcement pass.
- GET permissions displays missing rows as TRUE while the central permission checker can deny missing rows. A future consistency fix is recommended.
- This release was inspected and packaged, but not compiled here because the .NET SDK is unavailable in the execution environment. Run the build commands above before deployment.
