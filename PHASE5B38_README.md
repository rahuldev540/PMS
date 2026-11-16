# Phase 5B38 — SQL Server schema preflight for standalone conversion

Based on user-confirmed Phase 5B37. Adds **read-only** SQL Server schema inspection for HospitalId/ClinicId columns, foreign keys, and indexes, and a regression guard preventing destructive SQL from entering this inspection script. No EF model, controller, route, migration or database changes.

## Steps
1. `dotnet restore .\Pharmacy.sln`
2. `dotnet build .\Pharmacy.sln`
3. `python .\scripts\check_standalone.py`
4. In SSMS, choose your PMS database and execute `scripts/Inspect-LegacyHospitalSchema.sql`. Save results before designing the migration.
5. `python .\scripts\Report-LegacyHospitalDependencies.py --json --active-only > active-legacy.json` (type the command **once**; PowerShell errors if a pasted line has two `>` redirections).

No migration required for Phase 5B38. This is a preflight, not the removal itself. Do not delete historical migrations.
