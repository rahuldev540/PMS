# Phase 5B14 - Pharmacy Admin branch API response CMS metadata cleanup

Base: Phase 5B13. Removed legacy SourceSystem and ExternalBranchId response fields from branch listing, detail, creation and update responses in Controllers/PharmacyController.cs. No changes to database models, migrations, authentication or pharmacy-scoped branch ID logic. Other CMS/HospitalId dependencies remain.

## Steps
1. Back up Phase 5B13. Extract this ZIP into a new folder.
2. Run `dotnet restore .\Pharmacy.sln` then `dotnet build .\Pharmacy.sln`.
3. Run `dotnet run --project .\PharmacyOnly.Api.csproj`.
4. Test branch list, detail, create and update via Swagger; ensure frontend does not depend on SourceSystem or ExternalBranchId response properties.

No migration for this phase. .NET compilation not available in packaging environment.
