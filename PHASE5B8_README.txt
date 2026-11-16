PHASE 5B8 - Targeted Super Admin standalone scope cleanup

Changes:
- GET /api/pharmacy-super-admin/medicines now accepts pharmacyId instead of hospitalId.
- Medicine inventory stock joins now use PharmacyId and BranchId, not HospitalId.
- Medicine list returns PharmacyId instead of HospitalId.
- Pharmacy Admin activation duplicate check now uses PharmacyId and BranchId.
- UnifiedAuthController was reviewed: its remaining HospitalId occurrences are comments, not active access checks.

IMPORTANT: This is a limited phase. Many legacy HospitalId references remain in PharmacySuperAdminController and elsewhere; this ZIP is NOT yet a fully standalone PMS.
This environment does not have dotnet installed; compilation could not be verified here.

After extraction (back up current project first):
1. Extract into a NEW flat folder; do not overwrite your working project.
2. Open Visual Studio Developer PowerShell in the folder containing Pharmacy.sln.
3. dotnet restore .\Pharmacy.sln
4. dotnet build .\Pharmacy.sln
5. No EF migration is required for these controller-only changes.
6. Test GET /api/pharmacy-super-admin/medicines?pharmacyId=1 and Admin status endpoints.
7. Frontend callers of the Super Admin medicines endpoint must change hospitalId query parameter to pharmacyId.
