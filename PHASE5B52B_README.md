# Phase 5B52B – guarded migration correction

This package changes ONLY the pending migration `20261109010000_ConsolidatedStandaloneHospitalCleanup` relative to 5B52A. It adds fail-closed checks for composite foreign keys, composite indexes, filtered indexes, SQL modules, non-nullable columns, and foreign keys to Hospitals using other columns.

No new migration ID. The migration remains destructive and intentionally has no EF rollback. A full tested backup of PMSDB10 is mandatory.

## On Windows Developer PowerShell

1. Extract to a NEW folder; keep 5B52A and your database backup.
2. `Unblock-File .\.config\dotnet-tools.json`
3. `dotnet tool restore`
4. `dotnet restore .\Pharmacy.sln`
5. `dotnet build .\Pharmacy.sln`
6. `dotnet ef migrations script 20261107004500_RemoveUserHospitalColumn 20261109010000_ConsolidatedStandaloneHospitalCleanup --context AppDbContext -o standalone_review.sql`
7. Review the generated SQL and back up PMSDB10 before applying anything.

**DO NOT APPLY YET.** The archive is ZIP-tested, but this environment has not compiled the .NET project or executed SQL Server. The new guards deliberately abort on uncertain dependencies. Passing them does not certify every application workflow. Historical migrations are intentionally preserved.
