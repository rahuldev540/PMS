# Phase 5B9 — Standalone pharmacy branch controller cleanup

Based on Phase 5B8. Targeted changes:
- Remove CMS/standalone mode conditional selection from branch ID responses and branch CRUD lookups.
- Always resolve external branch IDs using PharmacyBranchIdentities scoped to PharmacyId.
- Always create pharmacy-scoped branch identity mapping when an Admin creates a branch.
- Return OperationMode = Standalone.
- Remove unreachable CMS manual prescription guard.
- Make Pharmacy Admin settings update pharmacy name without CMS mode branching.

No EF model/schema changes in this phase; no new migration expected.

## After extracting
1. Back up Phase 5B8; extract this ZIP to a separate directory.
2. `dotnet restore .\Pharmacy.sln`
3. `dotnet build .\Pharmacy.sln`
4. Test Pharmacy Admin branch list, get, create, update, status, delete and settings; test manual prescription.
5. Ensure pharmacy-scoped branch IDs stay 1,2,3 and are isolated across pharmacies.

Note: dotnet SDK unavailable in authoring environment, so this ZIP is not compile-tested. Existing CMS/HospitalId model and migration references remain for later phases. No database migrations were modified.
