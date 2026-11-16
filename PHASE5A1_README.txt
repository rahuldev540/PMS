PHASE 5A1 — LIMITED CMS PUBLIC-SURFACE CLEANUP

Based on Phase 4.
Changes:
- Removed CMS Integration aliases from UnifiedAuthController permission module normalization.
- Removed IsCmsIntegrated from PharmacyMasterController list/detail response projections.

This is NOT complete Phase 5A or a standalone PMS. CMS/HospitalId dependencies remain in the source.
No entity, schema, or migration modifications. Do not run migrations.
Build NOT verified in this environment: run dotnet restore .\Pharmacy.sln and dotnet build .\Pharmacy.sln.
Test Super Admin, Admin and Pharmacist login, permissions, pharmacy master list/detail.
