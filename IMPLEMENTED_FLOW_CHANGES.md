# Implemented Standalone PMS Flow Changes

## Super Admin
- Create a pharmacy independently with `POST /api/pharmacy-super-admin/pharmacies`.
- Pharmacy creation automatically creates `Main Branch` with `IsMainBranch=true`.
- Full pharmacy read/update/status/delete APIs are available under `/api/pharmacy-super-admin/pharmacies`.
- Pharmacy listing supports search/location filters without requiring PharmacyId.
- Create Pharmacy Admin independently with `POST /api/pharmacy-super-admin/admins`; the account starts Pending/unassigned.
- Existing `PUT /api/pharmacy-super-admin/admins/{id}/pharmacy-assignment` assigns the selected pharmacy and activates pharmacy access.
- Existing Super Admin branch CRUD remains. Main Branch cannot be hard-deleted.

## Pharmacy Admin / Pharmacists
- Existing Pharmacy Admin branch CRUD remains scoped to the Admin's assigned PharmacyId.
- Additional branches are sub-branches of the same PharmacyTenant; they do not create another pharmacy.
- Existing pharmacist CRUD, temporary-password welcome email, branch assignment/reassignment, permissions and status APIs remain.
- Existing forgot-password OTP/verify/reset flow remains.

## Inventory / Stock
Existing medicine, inventory, batches, purchase/GRN, stock transaction, stock adjustment, stock transfer, billing/dispensing, returns, realtime notifier and alert background-service code is retained. Stock remains branch-scoped and PharmacyId/BranchId are still used internally after location-based selection.

## Database
Migration added: `20260925100000_PharmacyMainBranchAndLocation`.
It adds pharmacy contact/location columns plus `Branch.IsMainBranch` and `Branch.UpdatedAt`.
If recreating the DB, keep both migrations and run `Update-Database`. Do not run `Add-Migration` first.
