# Phase 5B46 — Billing, import and branch settings CMS cleanup

Removed nullable legacy `HospitalId` from `MedicineImport`, `PharmacyBill`, and `PharmacyBranchSetting` models, their EF snapshot properties, and added a reversible migration guarded against populated legacy values. Existing pharmacy and branch keys are unchanged. Historical migrations remain intact.

## Apply

1. Back up PMSDB10 and your Phase 5B45 project. Confirm Phase 5B45 migration was applied before continuing.
2. `dotnet restore .\Pharmacy.sln`
3. `dotnet build .\Pharmacy.sln`
4. `python .\scripts\check_standalone.py`
5. Run SQL preflight: `SELECT (SELECT COUNT(*) FROM dbo.MedicineImports WHERE HospitalId IS NOT NULL) AS ImportsLegacy, (SELECT COUNT(*) FROM dbo.PharmacyBills WHERE HospitalId IS NOT NULL) AS BillsLegacy, (SELECT COUNT(*) FROM dbo.PharmacyBranchSettings WHERE HospitalId IS NOT NULL) AS SettingsLegacy;` All must be zero.
6. If needed: `Unblock-File .\.config\dotnet-tools.json` and `dotnet tool restore`.
7. Confirm connection string points to PMSDB10. Run `dotnet ef database update --context AppDbContext`.
8. Confirm migration `20261107000000_RemoveBillingImportSettingsHospitalColumns` appears in `dbo.__EFMigrationsHistory` and columns are absent.
9. Smoke test medicine import, pharmacy billing, branch settings, login, audit, pharmacy dashboard.

Migration is required. .NET compilation and live SQL migration were not available in the packaging environment.
