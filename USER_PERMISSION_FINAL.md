# Final user-specific permission flow

- PharmacyPermissionRole / PharmacyRolePermissions APIs are removed from Swagger/controller flow.
- Pharmacy Admin and Pharmacist authorization uses UserPermissions only.
- Every new Pharmacy Admin and Pharmacist receives all applicable modules with View/Create/Edit/Delete = true.
- Missing permission rows are treated as default true after account/pharmacy validation.
- Later explicit permission changes are preserved and are never reset by default creation logic.
- Admin creation no longer exposes PharmacyPermissionRoleId.
- Medicine bulk import flexible-header support is preserved.
- Branches is an explicit Pharmacy Admin permission module and defaults to true.
- Medicine bulk import permission checking works for both PharmacyAdmin and Pharmacist using UserPermissions.
