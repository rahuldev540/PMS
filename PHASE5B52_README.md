# Phase 5B52 — consolidated hospital removal (candidate)

This package **changes C# source and adds a guarded EF migration**. It has NOT been compiled here because dotnet SDK is unavailable. **Do not apply migration until `dotnet build` succeeds** and you have a full SQL Server backup.

1. `dotnet restore .\Pharmacy.sln`
2. `dotnet build .\Pharmacy.sln`
3. If build fails, DO NOT run migration. Share the compiler errors.
4. Back up PMSDB10 in SSMS and confirm backup is restorable.
5. `dotnet ef migrations list --context AppDbContext` and check last applied migration.
6. `dotnet ef database update --context AppDbContext`
7. Run SQL preflight again and smoke-test pharmacist/admin permissions, stock, prescriptions and billing.

The migration guards against non-null HospitalId values and nonempty Hospitals. It removes remaining HospitalId columns and their referencing FKs/indexes, and drops the empty Hospitals table. It does not yet remove other CMS-specific fields such as CmsHospitalId, SourceSystem or external IDs. Existing historical migrations are preserved. Dynamic SQL may fail if extra default constraints, check constraints, or dependent objects exist; in that case stop and share the error. The migration is not reversible via EF Down.
