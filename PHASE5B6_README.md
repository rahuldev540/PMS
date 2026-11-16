# Phase 5B6 — Pharmacy operation legacy-field cleanup

Built from Phase 5B5. Changes are limited to four files:

- `Services/PharmacyService.cs`: stop populating legacy HospitalId for newly created stock transactions, bills, and payments. Existing PharmacyId and BranchId assignments remain.
- `Controllers/ReturnsController.cs`: stop populating legacy HospitalId for two return-related records.
- `Controllers/PharmacyReportsController.cs`: omit HospitalId from the payment-report response.
- `Controllers/PharmacistAdminController.cs`: omit HospitalId from five pharmacist response projections.

## Important compatibility note

Legacy HospitalId properties and database columns are deliberately retained in this phase. The explicit assignments `pharmacist.HospitalId = null` remain, because these clear possible old hospital associations on reassignment. Other hospital-related references remain elsewhere. This is **not** a fully standalone database conversion.

No migration required; EF entities/mappings are unchanged. This package has been ZIP-integrity-checked but not built or runtime-tested here (dotnet SDK unavailable).

## Test

Run `dotnet restore .\Pharmacy.sln`, `dotnet build .\Pharmacy.sln`, `dotnet run --project .\PharmacyOnly.Api.csproj`. Test pharmacist creation/list/get/reassignment, prescription dispensing, bill creation, bill payment, returns, and payment reports. Verify tenant isolation for two pharmacies. Do not apply database migrations.
