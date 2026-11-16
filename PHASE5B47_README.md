# Phase 5B47 — User permission CMS cleanup

Removes legacy `HospitalId` and `Hospital` navigation from `UserPermission`, its EF snapshot mapping, and the `UserPermissions` database column (including its FK/index). The existing `UserId`, `Module`, and permission flags remain intact. Does not remove all CMS dependencies; remaining hospital-linked entities need later phases.

## Required steps (PowerShell, in extracted project root)

1. Back up your database and your working Phase 5B46 project. Do not drop the database.
2. `dotnet restore .\Pharmacy.sln`
3. `dotnet build .\Pharmacy.sln`
4. `python .\scripts\check_standalone.py` (if Python installed)
5. **Preflight in SSMS against your PMS database**:
   `SELECT COUNT(*) AS LegacyPermissionRows FROM dbo.UserPermissions WHERE HospitalId IS NOT NULL;`
   **Proceed only if the result is 0.** If not, stop and inspect those records; do not erase their scope automatically.
6. Verify the connection string points to the intended database.
7. `Unblock-File .\.config\dotnet-tools.json` (if Windows marks it blocked); `dotnet tool restore`.
8. `dotnet ef database update --context AppDbContext`
9. Verify `20261107003000_RemoveUserPermissionHospitalColumn` in `dbo.__EFMigrationsHistory`, then smoke-test login, Admin/Pharmacist permission retrieval and authorization, pharmacy/branch isolation.

**Migration required.** Build and SQL migration could not be run in this packaging environment (no dotnet or database connection). Migration is guarded to stop if any UserPermissions.HospitalId values exist.
