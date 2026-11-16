# Phase 5B52 migration safety fix

This replaces only migration 20261109010000_ConsolidatedStandaloneHospitalCleanup. It retains the existing source and EF snapshot.

Changes: guard computed/temporal columns, primary/unique constraints, check constraints and schema-bound dependencies; remove default constraints before dropping HospitalId columns; safely test whether dbo.Hospitals contains records even if absent. The migration is destructive and its Down method is unsupported.

## Before applying
1. Back up PMSDB10 and verify the backup.
2. Ensure no earlier version of this migration was applied.
3. Run dotnet build .\Pharmacy.sln.
4. Generate SQL for review: dotnet ef migrations script 20261107004500_RemoveUserHospitalColumn 20261109010000_ConsolidatedStandaloneHospitalCleanup --context AppDbContext -o standalone_review.sql
5. Inspect the script for tables and constraints that would be dropped, and confirm all HospitalId values are NULL.
6. Only after review, run dotnet ef database update --context AppDbContext.

This package was archive-validated but could not be compiled or tested against your SQL Server in this environment. The migration can intentionally fail on unexpected dependencies; do not bypass its guards.
