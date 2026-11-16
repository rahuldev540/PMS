# Phase 5B39 - Legacy hospital data pre-migration assessment

Base: Phase 5B38 (user-confirmed build and regression checks).

## Scope
Added `scripts/Inspect-LegacyHospitalData.sql` to count non-null legacy IDs per actual table/column, and list existing foreign keys to `Hospitals`. It is a **read-only inspection of persistent database data**. It creates and drops only a local SQL Server `#temp` table for report aggregation. It does not change any persistent table, index, FK, or record.

This is NOT an EF Core migration and does not remove HospitalId/ClinicId yet. Existing C# source, EF mappings, and APIs remain unchanged. Historical migrations are preserved.

## Run
1. Back up PMSDB10. Extract into a new directory.
2. `dotnet restore .\Pharmacy.sln`
3. `dotnet build .\Pharmacy.sln`
4. `python .\scripts\check_standalone.py`
5. In SSMS select **PMSDB10**, then File > Open > File > `scripts/Inspect-LegacyHospitalData.sql`, Execute.
6. Share all result grids (especially any NonNullLegacyValues > 0). Do not run manual DROP/ALTER commands.

No migration or `Update-Database` for this phase.
