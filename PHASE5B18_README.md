# Phase 5B18 - Notification audit CMS cleanup

Base: Phase 5B17.

Changed `Controllers/PharmacySuperAdminController.cs` only:
- Send notification audit no longer passes the legacy `HospitalId` to `AddAudit`.
- Mark-notification-read audit no longer passes `HospitalId`.
- Delete-notification audit no longer passes `HospitalId`, and removes the unnecessary local hospital ID.
- Existing branch ID attribution, notification storage, recipient selection, and responses are unchanged.

**Important:** HospitalId still exists in notification data and recipient selection. This is an incremental cleanup, NOT a complete standalone conversion. No database schema changes; no migration needed.

Run `dotnet restore .\Pharmacy.sln`, `dotnet build .\Pharmacy.sln`, and `dotnet run --project .\PharmacyOnly.Api.csproj`. Test notification send, mark-read, delete and audit. .NET SDK was unavailable in the packaging environment, so compile/test locally.
