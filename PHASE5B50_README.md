# Phase 5B50 consolidated preflight — NOT final conversion

This package retains the previously successful Phase 5B48A source and adds a complete exact-match source reference inventory and a read-only SQL Server preflight. It does NOT remove CMS fields or apply a migration. Do not treat it as the final standalone PMS.

1. Build the project with `dotnet build .\Pharmacy.sln`.
2. Run `scripts/standalone_database_preflight.sql` in SSMS against a backup/test database.
3. Review `STANDALONE_PRECHECK_REFERENCES.json` and the preflight output.
4. Back up the production database before any future destructive schema changes.

The next actual migration must preserve or explicitly resolve all rows linked to legacy HospitalId before dropping foreign keys/columns. Applied historical migration records should not be deleted.
