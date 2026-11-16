# Phase 4 — Patient and Prescription Scope Cleanup (Targeted)

Starting point: PMS_PHASE3_INVENTORY_STANDALONE.zip.

Changes: DoctorPrescriptionController now uses PharmacyId + BranchId instead of HospitalId to find and create prescriptions, and verifies the patient belongs to the pharmacy. Both create and update validate medicine ownership, branch and active status. No EF models, schema or migrations changed.

This is a targeted change, NOT full standalone completion. The Doctor route remains a legacy Doctor-role route; the normal pharmacy prescription/billing APIs were not rewritten. Legacy HospitalId columns and CMS logic elsewhere remain.

Migration: NONE for Phase 4. Do not run Add-Migration or Update-Database.

Steps: Extract ZIP, back up project and database, dotnet restore .\Pharmacy.sln, dotnet build .\Pharmacy.sln, dotnet run --project .\PharmacyOnly.Api.csproj. Verify pharmacy logins, prescriptions, dispensing, billing and cross-pharmacy isolation in Swagger.

Validation: ZIP structural/integrity checks only. .NET SDK unavailable here; compile and integration tests NOT verified.
