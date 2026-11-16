# Phase 5B5 — Pharmacy alert background tenant isolation

Based on Phase 5B4.

## Modified file
- Services/PharmacyAlertBackgroundService.cs

## Changes
- Reservation release finds inventory by Prescription.PharmacyId + BranchId + MedicineId rather than HospitalId.
- Low stock and automatic reorder alerts query inventories and existing alerts by PharmacyId + BranchId.
- Near-expiry alerts query batches by pharmacy and branch and use tenant-scoped duplicate detection.
- New alert rows are assigned PharmacyId rather than HospitalId.
- Skips settings with missing pharmacy or branch ownership.

## Limits
This is not the final removal of legacy HospitalId database columns. Other controllers, models and services still contain CMS/HospitalId references. No EF entity or schema changes were made. This archive has been checked for integrity but cannot be compiled here (dotnet SDK unavailable).

## Local verification
Run dotnet restore .\Pharmacy.sln and dotnet build .\Pharmacy.sln; then run the API and test reservation expiry, low-stock, reorder, and expiry alerts in two pharmacies with different branches. Check alert ownership in SSMS. No migration for this phase.
