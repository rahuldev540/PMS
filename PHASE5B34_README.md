# Phase 5B34 - Multi-file standalone nullable-default cleanup

Based on user-confirmed successful 5B33.

Removed 8 redundant `ClinicId = null` / `HospitalId = null` object-initializer entries across six active C# files. All target properties are nullable `int?`, whose CLR default is already null. These changes affect **newly constructed** login audit, notification, permission, and Super Admin records only.

**Important:** The two `pharmacist.HospitalId = null` assignments on **existing** pharmacist records remain in place, because those clear potentially stale legacy associations. Database columns and EF mappings remain unchanged.

No APIs removed, no schema migration required. Source regression checks included.

Run `dotnet restore .\Pharmacy.sln` and `dotnet build .\Pharmacy.sln`.

This package was source-checked but not compiled with dotnet here.
