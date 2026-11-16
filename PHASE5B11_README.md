# Phase 5B11 — Remove obsolete CMS tenant linkage from active EF model

Base: Phase 5B10.

Changes:
- Removed unused CMS integration properties from Models/PharmacyTenant.cs: IsCmsIntegrated, CmsHospitalId, CmsBranchId, CmsLinkedAt, CmsLinkedByUserId.
- Removed corresponding composite CMS index mapping from Data/AppDbContext.cs.
- Preserved pharmacy, branch, patient, inventory, authentication and all other model mappings.

IMPORTANT: This is NOT complete removal of HospitalId from the application. Historical migrations and other active model relationships remain. No migration is included in this phase. Existing CMS columns and index will remain in an already-created SQL Server database; removing these columns safely requires a separately reviewed EF migration (next phase). Do not drop columns manually.

Steps: back up 5B10 and database; extract ZIP; dotnet restore .\Pharmacy.sln; dotnet build .\Pharmacy.sln; test login and pharmacy/branch endpoints. No Update-Database for this phase.

Build status: not compiled here (SDK availability not verified).
