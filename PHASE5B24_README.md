# Phase 5B24 — Remove unused legacy hospital assignment validator

## Change
Removed the private `ValidateAssignment(int hospitalId, int branchId)` method from `Controllers/PharmacySuperAdminController.cs`. Repository-wide C# search found no call sites; the method was dead code and directly queried CMS Hospitals and hospital-bound Branches. No public endpoint or DTO is changed.

## After extraction
1. Back up Phase 5B23.
2. `dotnet restore .\Pharmacy.sln`
3. `dotnet build .\Pharmacy.sln`
4. `dotnet run --project .\PharmacyOnly.Api.csproj`
5. Smoke test Super Admin pharmacy and branch listing, notification APIs, and pharmacy-wise revenue.

No EF migration is needed: models, schema, and migrations were not modified. Legacy hospital dependencies remain elsewhere. .NET compilation was not available in the packaging environment.
