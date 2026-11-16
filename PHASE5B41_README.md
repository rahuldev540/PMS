# Phase 5B41 — Pharmacy tenant readiness before EF schema removal

Based on Phase 5B40. The user's PMSDB10 readiness screenshot reports 28 legacy columns, 0 populated legacy values, and 0 populated legacy columns. This clears the **data-value** gate only; EF relationships, foreign keys, indexes, and pharmacy tenant attribution still need coordinated changes.

## Included
- `scripts/Check-StandaloneTenantReadiness.sql`: read-only check for PharmacyId/BranchId presence, legacy foreign keys, and NULL PharmacyId counts in key tables.
- Source regression check for this SQL preflight.
- No EF model, API, schema or database changes; no migration in this phase.

## Instructions
1. Back up your SQL Server database and Phase 5B40 source folder.
2. Run `dotnet restore .\Pharmacy.sln`, `dotnet build .\Pharmacy.sln`, and `python .\scripts\check_standalone.py`.
3. In SSMS, select PMSDB10; File > Open > File > `scripts/Check-StandaloneTenantReadiness.sql`; press F5.
4. Share the result grids, especially `MissingPharmacyId` counts.
5. Do not run `Update-Database` or drop any columns yet.

## Next migration planning
After tenant checks, update EF entities, AppDbContext, service queries and migrations together. Preserve historical migration files. Review migration SQL before applying to a database backup; test pharmacist branch isolation, authentication, imports, prescriptions, and billing.
