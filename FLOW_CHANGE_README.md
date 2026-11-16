# Admin-owned pharmacy onboarding changes

1. Super Admin: `POST /api/pharmacy-super-admin/admins` creates a pending Admin without pharmacy assignment.
2. Pending Admin: `POST /api/auth/login` returns JWT and `RequiresPharmacyRegistration=true`.
3. Admin: `POST /api/pharmacy-admin/pharmacy` with `CreatePharmacyDto` creates pharmacy, main branch number 1 and assigns Admin atomically.
4. Admin logs in again to refresh JWT with pharmacy claims.
5. Admin: `POST /api/pharmacy/branches` generates branch numbers 2,3,... per pharmacy.
6. Admin: `POST /api/pharmacy-admin/pharmacists` uses the **globally unique internal Branch.Id**, NOT the pharmacy-local BranchNumber.

**Important:** Execute `BRANCH_NUMBER_SCHEMA.sql` against the existing SQL Server database before deploying, or generate and apply an equivalent EF Core migration. This package has NOT been compiled or integration tested because the .NET SDK is unavailable in the execution environment. Do not run both SQL and a duplicate EF migration. Existing Super Admin manual assignment and pharmacy creation endpoints are retained for backward compatibility; remove or restrict them if the new workflow must be exclusive. All existing foreign keys must continue to reference internal `Branch.Id`.
