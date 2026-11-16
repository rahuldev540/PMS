# Phase 2: Standalone pharmacy / branch API cleanup

Source: PMS_PHASE1_AUTH_STANDALONE.zip.

## Changed
- Removed GET /api/pharmacy-super-admin/hospitals.
- Removed PATCH /api/pharmacy-super-admin/hospitals/{id}/status.
- Removed GET /api/pharmacy-super-admin/hospitals/{hospitalId}/branches.
- Removed CMS integration fields from GET /api/pharmacy-super-admin/pharmacies/overview responses.
- Preserved pharmacy/branch endpoints, admin endpoints, and operational code.

## Important scope
This is **not** the final CMS removal. Legacy CMS columns, entities, relationships, and hospital references in other controllers are intentionally retained for later phases to avoid changing the database during this phase. Some Super Admin branch modification routes also remain and must be reviewed separately for role policy.

## Migration
No migration is needed for this phase: no EF entity or database mapping changes. Do not run Add-Migration or Update-Database for this phase.

## After extraction
1. Back up your working project and database.
2. Extract to a new directory. Project files are at the ZIP root.
3. Run `dotnet restore .\Pharmacy.sln`.
4. Run `dotnet build .\Pharmacy.sln`.
5. If build succeeds, run `dotnet run --project .\PharmacyOnly.Api.csproj`.
6. In Swagger, confirm the three removed hospital routes are absent, and test `GET /api/pharmacy-super-admin/pharmacies/overview` and `GET /api/pharmacy-super-admin/branches` with Super Admin authorization.
7. Test Admin pharmacy and branch APIs and pharmacist login for regressions.

Build not verified here: dotnet SDK is unavailable in the authoring environment.
