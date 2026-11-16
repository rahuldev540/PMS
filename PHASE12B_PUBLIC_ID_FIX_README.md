# Phase 12B – Public branch ID consistency (targeted corrections)

## Changed
- Controllers/PharmacySuperAdminController.cs: GET /api/pharmacy-super-admin/admins and GET /api/pharmacy-super-admin/admins/{id} now project public pharmacy-scoped branch IDs for both branchId and nested branch.id. Missing mappings produce an error rather than exposing internal keys.
- Controllers/PatientCompleteHistoryController.cs: no longer emits InternalPatientId; missing public branch identity no longer silently returns the internal branch ID.
- Controllers/PharmacyController.cs: standalone prescription creation response no longer emits InternalPatientId.

## Not changed
- Database internal foreign keys, JWT claims, permissions, stock movements, billing relationships.
- Public prescription/bill ID mappings are not addressed by this targeted release.
- No new APIs or EF migration.

## Verification
- Build and runtime integration tests must be performed on a machine with .NET 8 SDK and a test database. The packaging environment did not have dotnet.
- In Swagger test GET /api/pharmacy-super-admin/admins and GET /api/pharmacy-super-admin/admins/4; for pharmacy 9 Main Branch branchId and branch.id should be 1.
- Verify another pharmacy has its own Main Branch ID 1; test Pharmacist login and branch-scoped inventory, stock transfer, prescriptions.
- Test GET /api/patients/complete-history?mobile=... and standalone prescription creation responses for absence of InternalPatientId.
- Check for client dependencies on removed InternalPatientId before deploying frontend changes.

## Extract / run
`dotnet restore .\\Pharmacy.sln`
`dotnet build .\\Pharmacy.sln`
`dotnet run --project .\\PharmacyOnly.Api.csproj`

Keep a backup of your database and previous project before deployment.
