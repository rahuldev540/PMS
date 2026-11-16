# PMS standalone cleanup status

This archive is a targeted compile-error correction, NOT a fully standalone conversion.

Fixed: Services/UserPermissionService.cs no longer references user.HospitalId on an anonymous projection that does not select it.

The permission PUT error shown in the browser is caused by the frontend sending the module `CMS Integration`, which the backend rejects as invalid. Remove that module from the frontend's permission list and PUT payload. The frontend is not included in this backend archive.

IMPORTANT: CMS/HospitalId references still exist in controllers, services, models, DbContext and migrations. Do not assume this ZIP removes CMS integration. No schema changes or migrations were made. Historical EF migrations must be handled carefully, not blindly deleted.

After extraction: dotnet restore .\\Pharmacy.sln; dotnet build .\\Pharmacy.sln. Build was not run here because the .NET SDK is unavailable in this environment. Do not apply database migrations on the strength of this archive.
