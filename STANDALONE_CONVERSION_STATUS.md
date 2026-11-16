# Standalone PMS conversion status

This is an INCOMPLETE conversion, not a verified standalone release.

## Change made
- Controllers/PharmacistInventoryController.cs: inventory queries now scope by PharmacyId and BranchId rather than requiring HospitalId. Inventory permission lookup now scopes by authenticated UserId and module; confirm your permissions model and provisioning logic.

## Outstanding work before production
- RoleDashboardController and RealtimePharmacyOperationsController still use HospitalId.
- Other CMS-derived controllers, services, entities and migrations require a comprehensive audit.
- Confirm all pharmacy JWT claims, branch-selection logic, per-pharmacy IDs, and authorization policies.
- Test migrations and end-to-end flow from Super Admin to billing.
- This environment does not have the dotnet SDK, so the project has NOT been compiled or integration-tested.

Do not deploy this package as a completed standalone migration.
