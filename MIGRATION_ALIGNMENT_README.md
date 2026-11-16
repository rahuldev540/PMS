# Migration alignment

This project is aligned to the existing PMSDB migration history:

- 20260922110912_InitialCreate
- 20260923103550_AddStandalonePharmacyAddress
- 20260924072429_MakeUserPermissionHospitalNullable

Those three migration classes are intentionally no-op because the existing database already records them as applied.

`20260924101542_StandalonePharmacyCmsReady` is the only pending migration. Its SQL is idempotent for the known existing PMSDB state: it checks columns/indexes before adding/removing them, preserves legacy CMS mapping values into CmsHospitalId/CmsBranchId when present, and keeps HospitalId nullable for future CMS integration.

Run in Package Manager Console:

1. `Get-Migration`
2. Confirm the first three show `True` and StandalonePharmacyCmsReady shows `False`.
3. `Update-Database`

Do not delete PMSDB and do not manually edit __EFMigrationsHistory.
