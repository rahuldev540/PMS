# Phase 5B19 - Notification read audit residual HospitalId fix

Base: Phase 5B18.

Changed `Controllers/PharmacySuperAdminController.cs`: the single-notification mark-as-read audit still passed `notification.HospitalId` in 5B18. This is now replaced with `null`, consistent with the notification send and delete audit calls. BranchId attribution is preserved.

No entity models, migrations, schema, notification recipient logic or other APIs were changed. **No EF migration required.** Other CMS/HospitalId references remain and require future cleanup.

After extraction: `dotnet restore .\Pharmacy.sln`, `dotnet build .\Pharmacy.sln`, `dotnet run --project .\PharmacyOnly.Api.csproj`. Test marking a notification as read and inspect audit logs. This ZIP was checked for archive integrity but could not be .NET-compiled in this environment.
