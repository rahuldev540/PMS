# User-specific permission update

This build uses `UserPermissions` as the authorization source for Pharmacy Admin access.

- Super Admin -> Pharmacy Admin permissions:
  - GET `/api/pharmacy-super-admin/admins/{id}/permissions`
  - PUT `/api/pharmacy-super-admin/admins/{id}/permissions`
- Pharmacy Admin self permissions:
  - GET `/api/pharmacy-admin-auth/permissions`
- Pharmacy Admin -> Pharmacist permissions continue to use `UserPermissions`:
  - GET `/api/pharmacy-admin/pharmacists/{id}/permissions`
  - PUT `/api/pharmacy-admin/pharmacists/{id}/permissions`
- New Pharmacy Admin permissions default to View/Create/Edit/Delete = true.
- New Pharmacist permissions default to all true when no explicit permissions are supplied; supplied permissions are stored per user.
- `PharmacyPermissionRole` / `PharmacyRolePermissions` remain in the database/code for backward compatibility, but `PharmacyPermissionChecker` no longer uses them for Pharmacy Admin authorization.

Admin and pharmacy GET responses were expanded:
- Admin GET/list includes assigned pharmacy details (name, email, phone, address, city, district, state, country, postal code, status) and branch details.
- Pharmacy GET/list includes assigned Pharmacy Admin details.
- Pharmacy overview includes pharmacy contact/address fields.

Note: The execution environment used to prepare this ZIP does not have the .NET SDK installed, so a local `dotnet build` could not be run here. Build the project in Visual Studio/.NET 8 after extraction.
