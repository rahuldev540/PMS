# PMS branch-scoped import update (source package)

## Changes
- Removed BranchNumber from Branch model, EF model and API controller references. New migration `20261016113000_RemoveBranchNumber` drops the index and column. Old historical migrations are intentionally retained.
- Branch creation now assigns pharmacy-local BranchId through PharmacyBranchIdentities with SQL Server transaction lock.
- Pharmacist assignment and branch APIs resolve pharmacy-local IDs using PharmacyBranchIdentities.
- Medicine Import `POST /api/medicine-import/validate`: multipart form `File` plus optional `BranchId` (pharmacy-local). PharmacyAdmin may choose an active branch of their own pharmacy; Pharmacist is restricted to their assigned branch. If admin omits BranchId, existing JWT branch is used.
- Import operations (commit, status, rows, errors) resolve destination branch from the pharmacy-local import ID; pharmacists cannot operate on another branch's import.
- `GET /api/medicine?branchId=2` and `GET /api/inventory?branchId=2` let PharmacyAdmin view a selected branch's catalogue/inventory; pharmacist cannot select another branch. Existing no-parameter GETs use JWT branch.
- Medicine and inventory service filters already enforce PharmacyId + internal BranchId.

## IMPORTANT BEFORE DEPLOYMENT
This source was patched in a container WITHOUT the .NET SDK, and was NOT compiled, integration-tested, or run against your database. Treat it as a candidate update requiring a developer build, review, and test. Back up PMSDB10 first. Do not run Update-Database before successful build and migration review.

1. Extract, restore packages, run `dotnet build` (or Visual Studio Build).
2. Inspect all changes and migration `20261016113000_RemoveBranchNumber.cs`.
3. Back up the database. Confirm earlier migrations applied.
4. Apply `Update-Database -Context AppDbContext` only after verification.
5. Verify branch creation, pharmacist assignment, admin import to branch 2, pharmacist rejection of branch 2 if assigned to branch 1, commit, GET catalogue/inventory, CMS endpoints, and preexisting data.
6. Review all other medicine/inventory endpoints for optional admin branch switching. Only the two primary GET endpoints were extended.

This package is not a production-ready verified release.
