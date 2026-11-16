# Phase 3 — Inventory / Stock / Purchasing standalone cleanup (targeted)

This is a targeted code-only cleanup built on the Phase 2 archive.

Changes: removed explicit assignments to legacy `HospitalId` properties when creating inventory, batches, purchase orders, stock adjustments and transfers. Previously these assignments were either `null` or `OptionalHospitalId`; after this change, nullable `HospitalId` properties retain their default `null`. The database columns and EF Core models remain unchanged for compatibility.

**No migration for this phase** because the entity schema and mappings have not been changed. Do not run Add-Migration or Update-Database for Phase 3.

**Not yet fully standalone:** CMS/hospital references remain in other files and EF Core models; further phases must handle those safely. No dotnet SDK was available in the creation environment; compilation and integration tests are not verified.

## After extraction
1. Back up your existing project and database. Extract into a fresh directory.
2. Restore: `dotnet restore .\Pharmacy.sln`
3. Build: `dotnet build .\Pharmacy.sln`
4. Run: `dotnet run --project .\PharmacyOnly.Api.csproj`
5. Test Admin purchase order creation, inventory adjustments, stock transfer, Excel medicine import and pharmacist branch stock access. Confirm the records have the correct PharmacyId and BranchId.
6. If build or API tests fail, stop and share the error; do not deploy.
