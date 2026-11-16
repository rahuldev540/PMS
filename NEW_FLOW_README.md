# Corrected Pharmacy Admin onboarding flow

1. Super Admin creates a Pharmacy Admin: `POST /api/pharmacy-super-admin/admins`.
2. New Admin logs in: `POST /api/auth/login` (Pending, no pharmacy). JWT permits onboarding only.
3. Admin registers own pharmacy: `POST /api/pharmacy-admin/pharmacy` with Admin bearer token and pharmacy name/contact/address.
4. Registration atomically creates pharmacy, Main Branch (BranchNumber=1), and assigns Admin to pharmacy and main branch.
5. Admin logs in again to refresh assignment claims.
6. Admin creates sub-branches using `POST /api/pharmacy/branches` (numbers 2,3,... per pharmacy).
7. Admin creates pharmacists via existing pharmacist management APIs and assigns them to branches belonging to the same pharmacy.

Super Admin pharmacy/branch POST and manual pharmacy assignment endpoints have been removed. Super Admin read/management APIs remain.

`20261015064854_AddBranchNumberPerPharmacy` migration is included. If it is already applied in your local database, do NOT re-run SQL schema scripts. Do not generate another migration with the same name.

SECURITY: Before production, audit every Admin/Pharmacist controller for tenant isolation and restrict pending onboarding tokens to onboarding/password APIs via a centralized authorization policy or middleware. Role authorization alone is not sufficient to block all unrelated endpoints.

This package has not been compiled or integration-tested in this environment (no .NET SDK installed). Back up the database and validate the login, registration, branch, pharmacist, and cross-tenant scenarios in Visual Studio.
