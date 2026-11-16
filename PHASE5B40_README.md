# Phase 5B40 — CMS schema removal readiness gate

Based on Phase 5B39. The screenshot shows 0 non-null values in the first 13 rows, but does not show all 37 legacy columns. This phase adds a single summary query over ALL legacy columns and inventories foreign keys/indexes.

## What changed
- `scripts/Check-LegacyMigrationReadiness.sql`: read-only summary, complete column counts, FKs, indexes.
- `scripts/check_standalone.py`: regression guard for the new script.
- No C# application behavior or EF model changes; no database migration.

## Windows steps
1. Back up project and SQL database.
2. `dotnet restore .\Pharmacy.sln`
3. `dotnet build .\Pharmacy.sln`
4. `python .\scripts\check_standalone.py`
5. In SSMS, select PMSDB10 and use File > Open > File to open `scripts/Check-LegacyMigrationReadiness.sql`; press F5.
6. Share the summary result (TotalPopulatedLegacyValues, PopulatedColumns, DataReadiness), and any rows with PopulatedRows > 0.

Do not run `Update-Database` or drop columns/tables in this phase. A later coordinated migration must first remove active EF references and relationships.
