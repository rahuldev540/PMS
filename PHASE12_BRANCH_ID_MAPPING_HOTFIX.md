# Phase 12 – Branch ID response mapping hotfix

Based on PMS_STANDALONE_PHASE12_LOGIN_PERFORMANCE.zip.

## Changed files
- Controllers/UnifiedAuthController.cs: `POST /api/auth/login` returns pharmacy-scoped public `user.branchId` while retaining the internal branch FK for JWT and audit logging.
- Controllers/PharmacyAuthController.cs: `GET /api/pharmacy-auth/assignment-status` returns public `branchId`.
- Controllers/PharmacyAdminAuthController.cs: Admin assignment-status response returns public `branchId` when assigned.

The existing `PharmacyPublicBranchMapper` is reused. No schema changes or new endpoints.

## Safety
Do not rewrite Users.BranchId or JWT branch claims to public IDs. Existing controllers use internal IDs for joins, filtering and authorization. If a branch identity mapping row is missing, the mapper raises an error rather than silently returning the wrong ID; investigate identity data rather than guessing.

## Validation
1. Back up database and previous project.
2. `dotnet restore .\\Pharmacy.sln`
3. `dotnet build .\\Pharmacy.sln`
4. `dotnet run --project .\\PharmacyOnly.Api.csproj`
5. Test `POST /api/auth/login` for pharmacists assigned to main branch and Getha. Verify response `user.branchId` equals 1 and 2, respectively, and `branchName` agrees.
6. Test `GET /api/pharmacy-auth/assignment-status` with pharmacist JWT; verify matching public ID.
7. Test Admin assignment status, Admin dashboard, pharmacist stock and prescription endpoints to confirm branch isolation.
8. If any additional response exposes internal IDs, record its endpoint and payload. A source scan is not a substitute for integration tests of every API.

No EF migration required. No new API endpoints.

## Verification limits
Source-level review and ZIP integrity verification only; .NET SDK and live SQL Server are unavailable in this environment, so build and live integration tests must be run on your machine.
