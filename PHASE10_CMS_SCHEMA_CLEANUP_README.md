# Phase 10 — standalone PMS database schema repair

## Verified issue
The uploaded Phase 9 ZIP has a standalone PharmacyTenant model, but its EF model snapshot still includes IsCmsIntegrated, CmsHospitalId, CmsBranchId, CmsLinkedAt, CmsLinkedByUserId and a composite CMS index. The SQL screenshot confirms those columns are still in PMSDB10.dbo.Pharmacies. The POST pharmacy 500 is caused by the NOT NULL IsCmsIntegrated column.

## Changes
- Added idempotent EF migration 20261110120000_RemoveRemainingPharmacyCmsColumns to drop the five legacy columns and associated ordinary indexes/default constraints from dbo.Pharmacies.
- Updated AppDbContextModelSnapshot to reflect the standalone PharmacyTenant model.
- Historical migrations retained for existing migration-chain compatibility. They contain legacy CMS text and are not runtime integrations.

## Apply
1. BACK UP PMSDB10. Stop API and ngrok process.
2. Extract the ZIP into a new directory (files are at ZIP root).
3. Verify connection strings and EF environment target PMSDB10 on the intended SQL Server instance.
4. From project root run `dotnet restore`, `dotnet build`, then `dotnet ef database update --context AppDbContext`.
5. Restart backend and ngrok, login as Pharmacy Admin, retry POST /api/pharmacy-admin/pharmacy.
6. Run the verification SQL below.

```sql
USE PMSDB10;
SELECT c.name FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id
WHERE t.name='Pharmacies' AND (c.name LIKE '%Cms%' OR c.name LIKE '%Hospital%');
SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC;
```

IMPORTANT: This migration targets the five known columns on Pharmacies. It does not prove every other legacy CMS artifact is absent from all tables or historical source. Check the full schema before claiming full cleanup. SQL schema and API are separate from frontend caches.

No local dotnet compiler was available in this environment; build and migration must be verified in Visual Studio before deployment.
