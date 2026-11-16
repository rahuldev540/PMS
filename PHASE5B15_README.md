# Phase 5B15 — Super Admin notification list CMS response cleanup

Base: Phase 5B14.

Changed Controllers/PharmacySuperAdminController.cs notification list projection to omit HospitalId and HospitalName. The endpoint no longer queries the legacy Hospitals table solely to display the hospital name in its list response. Notification storage, creation, filtering, recipient permissions and database schema are unchanged. Other CMS and HospitalId dependencies remain and require later phases.

## After extraction
1. Back up your Phase 5B14 project and database; extract into a new folder.
2. Run `dotnet restore .\Pharmacy.sln` and `dotnet build .\Pharmacy.sln`.
3. Run `dotnet run --project .\PharmacyOnly.Api.csproj`.
4. Test the Super Admin notifications listing in Swagger and check the frontend no longer expects `hospitalId` or `hospitalName` in this listing.

No EF migration is needed: only an API response projection changed.
Build not executed in packaging environment (dotnet SDK unavailable).
