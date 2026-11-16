# Phase 5B16 — Super Admin notification creation response cleanup

Base: Phase 5B15.

## Changed
- Removed `HospitalId` from the response returned when Super Admin sends a notification (`POST /api/pharmacy-super-admin/notifications`).
- Retained the existing notification creation, audit, and storage logic to avoid changing database behavior in this phase.
- No database migration is included or required for this response-only change.

## Not yet changed
- HospitalId and CMS references elsewhere in active code and EF Core/database schema remain. This phase is NOT a complete standalone conversion.
- The frontend should not expect `notification.hospitalId` in the send response.

## Run locally
```
dotnet restore .\Pharmacy.sln
dotnet build .\Pharmacy.sln
dotnet run --project .\PharmacyOnly.Api.csproj
```

Test sending and listing Super Admin notifications, and confirm branch and authentication behavior.

ZIP integrity verified; .NET SDK compilation not performed in this environment.
