# PMS standalone consolidation — safety audit package

**Status: NOT a completed standalone conversion.** This package is based on the last user-confirmed building Phase 5B48A project. It adds an exhaustive source audit and read-only SQL inventory. It deliberately does not delete CMS data, change the EF model, or introduce an untested migration.

## Why no destructive bulk migration yet?
HospitalId still occurs in multiple entity models, AppDbContext, EF snapshot, and old migration files. Some hospital-linked records may exist. Removing the columns without proving data ownership and reconciling relationships could destroy clinical and financial history.

## Run
1. `dotnet restore .\Pharmacy.sln`
2. `dotnet build .\Pharmacy.sln`
3. `python .\scripts\audit_standalone_bulk.py`
4. Run `scripts/standalone_schema_inventory.sql` in SSMS (read-only).
5. Share the audit JSON and SQL results before attempting destructive changes.

**Do not apply new migrations from this package.** Phase 5B48's `RemoveUserHospitalColumn` migration has already been applied in your environment.
