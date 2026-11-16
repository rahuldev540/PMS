# Phase 12D – additive notification endpoints

Source: Phase 12C regenerated backend ZIP. Existing source files, schema, migrations and records are unchanged. Only `Controllers/PharmacyRecipientNotificationsController.cs` is new.

## New routes (JWT required)
- PharmacyAdmin: GET `/api/pharmacy-admin/notifications?status=all&page=1&pageSize=20`
- PharmacyAdmin: PATCH `/api/pharmacy-admin/notifications/{id}/read`
- Pharmacist: GET `/api/pharmacist/notifications?status=all&page=1&pageSize=20`
- Pharmacist: PATCH `/api/pharmacist/notifications/{id}/read`
- PharmacyAdmin or Pharmacist: GET `/api/notifications/unread-count`
- PharmacyAdmin or Pharmacist: PATCH `/api/notifications/mark-all-read`

Role restrictions and pharmacy/branch targeting use authenticated internal IDs, not caller-supplied tenant IDs. Unknown notification IDs return 404.

## Important existing schema limitation
The existing `PharmacyNotifications` table has a single global `IsRead` and `ReadAt` per notification, not per recipient. Consequently, marking an `All` or `Branch` notification read changes its read status for other recipients too. This additive release intentionally does not change the schema or existing notification records. If per-user read tracking is needed, a separate additive read-receipt table and migration should be approved before deployment. Existing Super Admin notification behavior remains unchanged.

`All` notifications are global because the existing notification model does not store a PharmacyId. User notifications are limited to the authenticated user; branch notifications are limited to the authenticated pharmacy and, for pharmacists, the assigned branch. No new notification sending APIs were added.

## Build and test
```
dotnet restore .\Pharmacy.sln
dotnet build .\Pharmacy.sln
dotnet run --project .\PharmacyOnly.Api.csproj
```
No EF migration required. Test with Admin and Pharmacist JWTs. No .NET SDK was available in the generation environment, so compile and live tests are pending.
