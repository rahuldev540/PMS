STANDALONE PMS — PARTIAL SOURCE PATCH (UNVERIFIED)

This is NOT a completed removal of all CMS/HospitalId code.
Changes made:
- Removed CMS hospital requirement from PharmacyPermissionChecker.
- Default user permissions now always use null HospitalId.
- Removed CMS wording from assignment middleware.
- Pharmacist login returns standalone mode and null CMS/hospital fields (legacy DTO fields remain).

IMPORTANT: CMS endpoints, model properties, migrations, and other HospitalId usages remain.
No .NET SDK was available for build verification. Do not deploy this patch as a completed standalone migration.
To test in Visual Studio: dotnet restore .\Pharmacy.sln ; dotnet build .\Pharmacy.sln
