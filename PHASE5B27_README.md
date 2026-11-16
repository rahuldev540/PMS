# Phase 5B27 — Remove disabled legacy hospital revenue reporting

## What changed
- Removed three `[NonAction]` methods in `Controllers/PharmacySuperAdminController.cs`: `RevenueReport`, `ExportRevenueExcel`, `ExportRevenuePdf`. Their HTTP route attributes were commented out, so these were not exposed as API endpoints.
- Removed their private `BuildRevenueReportAsync` helper and four private report-only DTO types.
- Eliminated the legacy `_db.Hospitals` joins, HospitalId filtering, and hospital-specific revenue reporting code in this disabled block.
- Preserved the ACTIVE `GET /api/pharmacy-super-admin/reports/pharmacy-revenue` endpoint and active Super Admin dashboard.
- No EF entities, migrations, or database schema changed.

## Validation
Run `dotnet restore .\Pharmacy.sln`, `dotnet build .\Pharmacy.sln`, and `dotnet run --project .\PharmacyOnly.Api.csproj`. Test active pharmacy-revenue report, Super Admin dashboard, and Swagger.

## Important
ZIP integrity checked, but .NET compilation not available in the build environment. More legacy HospitalId references remain elsewhere.
