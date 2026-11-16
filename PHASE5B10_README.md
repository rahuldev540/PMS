# Phase 5B10 — unused CMS controller helper and DTO cleanup

Base: Phase 5B9.

Changes:
- Removed unused inherited CMS claim helper properties `IsCmsIntegrated`, `OptionalHospitalId`, and `HospitalId` from PharmacyApiControllerBase.
- Removed unused DTO records `LinkPharmacyToCmsDto` and `PharmacyCmsIntegrationDto`.
- Preserved all active models, EF mappings, migrations, and pharmacy/branch ID logic.

Verification: repository-wide static references were checked for these symbols. Build and runtime tests have NOT been performed in this environment.

After extraction (flat structure):
1. Back up your working 5B9 project.
2. Run `dotnet restore .\Pharmacy.sln` and `dotnet build .\Pharmacy.sln`.
3. Run `dotnet run --project .\PharmacyOnly.Api.csproj`.
4. Smoke-test Super Admin login, Pharmacy Admin login, pharmacist login, branch CRUD, and Swagger.

Migration: NOT needed for this phase (no EF model/schema changes).

Remaining: CMS/HospitalId are still present in active controllers, entities, EF configuration, and migrations. Do not drop columns yet.
