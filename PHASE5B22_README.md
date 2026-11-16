# Phase 5B22 — Notification request DTO cleanup

Removed the obsolete optional `HospitalId` property from `SendPharmacyNotificationDto`. The notification creation controller no longer uses it (Phase 5B21); clients should send only Title, Message, RecipientType, BranchId and UserId. No database model, schema, or migration changed.

## Validate

1. `dotnet restore .\Pharmacy.sln`
2. `dotnet build .\Pharmacy.sln`
3. `dotnet run --project .\PharmacyOnly.Api.csproj`
4. Check Swagger notification request body has no HospitalId; test All, Branch and User notifications.

**Important:** This is an API request contract change. Update frontend request models if they still include HospitalId. Other hospital-based revenue and reporting code remains active and requires a separate redesign before removal.
