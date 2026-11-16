# Phase 5B32: active Super Admin audit search isolation

Based on user-confirmed successful Phase 5B31.

- Removed the **active** legacy CMS Clinic navigation-name search predicate from `BuildAuditLogQuery`. This prevents a standalone pharmacy audit search from joining the old Clinic/Hospital table.
- Preserved search by user name, action, system action, IP, role, branch name and user email.
- Preserved audit filtering, pagination, active revenue endpoints and all existing routes.
- Documented the unused `hospitalId` compatibility arguments in `UserPermissionService` without changing public method signatures.
- Extended the read-only regression checker to reject reintroduction of Clinic/Hospital lookup in the active audit query.

Behavioral note: Audit searching by *old clinic name* is intentionally no longer supported. No schema changes or EF migrations.

Run `dotnet restore .\Pharmacy.sln` then `dotnet build .\Pharmacy.sln`, and test Super Admin audit logs search with user email, branch name, action and role.

ZIP and static assertions verified; .NET SDK is unavailable in the packaging environment, so compilation must be verified locally.
