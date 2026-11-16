# Phase 5B44 — Remove four operational HospitalId columns

Based on Phase 5B43 (user confirmed EF migration applied to PMSDB10). This phase removes the nullable, unused `HospitalId` property and database column from `Inventories`, `Medicines`, `StockTransactions`, and `PharmacyNotifications`. These models have no active C# consumers of their HospitalId property. It replaces the notification index `(HospitalId, BranchId, UserId)` with `(BranchId, UserId)` and updates the EF configuration. It also updates the EF model snapshot and adds a manually authored, data-guarded migration `20261106233000_RemoveOperationalHospitalColumns`. Historical migrations remain untouched.

## Before applying

1. Back up PMSDB10 and the Phase 5B43 project. Confirm app connection string targets PMSDB10.
2. In SSMS run: `SELECT COUNT(*) FROM dbo.Inventories WHERE HospitalId IS NOT NULL;` and repeat for `Medicines`, `StockTransactions`, `PharmacyNotifications`. All must be zero. Migration independently guards against non-null data.
3. `dotnet restore .\Pharmacy.sln`
4. `dotnet build .\Pharmacy.sln`
5. `python .\scripts\check_standalone.py`
6. Inspect the migration script before running: `dotnet ef migrations script 20261106230000_RemoveAuditLogClinicLink 20261106233000_RemoveOperationalHospitalColumns --context AppDbContext -o phase5b44.sql`
7. `dotnet ef database update --context AppDbContext`
8. Verify four columns absent in SSMS; check `__EFMigrationsHistory` and smoke-test medicine import, inventory, stock movement, notification, audit, and billing.

No APIs or tenant IDs are intentionally changed. Other legacy HospitalId fields and the Hospitals table remain pending. The ZIP was source-checked; dotnet SDK is unavailable in the packaging environment, so local build and migration testing are mandatory.
