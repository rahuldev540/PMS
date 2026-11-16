# Phase 5B45 — Commerce hospital column removal

Removes nullable, unused HospitalId fields from Suppliers, PurchaseOrders, GoodsReceipts and PharmacyPayments, with corresponding EF snapshot changes and a guarded reversible migration. Does not modify PharmacyId, BranchId, billing relationships, purchase order logic, or historical migrations.

## Windows steps

1. Back up PMSDB10 and the previous project.
2. Extract into a fresh directory.
3. `dotnet restore .\Pharmacy.sln`
4. `dotnet build .\Pharmacy.sln`
5. `python .\scripts\check_standalone.py`
6. In SSMS check all four legacy fields are NULL, before migration.
7. `Unblock-File .\.config\dotnet-tools.json` if Windows blocks downloaded tool manifest; then `dotnet tool restore`.
8. Confirm API connection string points to PMSDB10; run `dotnet ef database update --context AppDbContext`.
9. Verify `20261106234500_RemoveCommerceHospitalColumns` in `__EFMigrationsHistory` and absence of HospitalId in the four tables.
10. Smoke test suppliers, purchase orders, goods receipts and pharmacy payments.

No automatic build was possible in the packaging environment. Historical migrations are intentionally retained.
