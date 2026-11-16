# Phase 5B25 — Standalone permission cleanup (batched)

Based on Phase 5B24. Changed two C# files, covering three standalone permission cleanups:

1. Models/PharmacyPermissionSeeder.cs: stop selecting Users.HospitalId for startup permission seeding.
2. Models/PharmacyPermissionSeeder.cs: seed new UserPermission rows with HospitalId = null (legacy nullable column remains).
3. Models/PharmacyPermissionSeeder.cs and Helpers/PharmacyPermissionChecker.cs: remove special CMS/CMS Integration permission alias normalization. Existing permission rows are NOT deleted and retain their values.

No database model or migration changes. Existing CMS/HospitalId references remain elsewhere; this is not complete standalone conversion.

## Local verification
```
dotnet restore .\Pharmacy.sln
dotnet build .\Pharmacy.sln
dotnet run --project .\PharmacyOnly.Api.csproj
```

Test admin/pharmacist login, permission seeding, permission list and edits, branch isolation. No migration needed.

ZIP integrity verified; .NET SDK build was not run in packaging environment.
