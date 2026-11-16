# Phase 5B2 — Pharmacy-scoped medicine operations (incremental)

Source: Phase 5B1.

Changed: Controllers/RealtimePharmacyOperationsController.cs
- Barcode lookup, medicine control update and medicine recall lookup now use Medicine.PharmacyId and BranchId rather than Medicine.HospitalId.
- Recall batch lookup also checks Inventory.PharmacyId and BranchId.
- These medicine endpoints reject missing pharmacy/branch claims.

NOT YET CONVERTED: shift management, supplier invoices, split payments, medicine recall event/storage and other legacy HospitalId paths. Do not assume these are standalone or safely isolated; test them separately before use.

No model/mapping/schema changes. No migration required for this increment.

Build not tested here (dotnet SDK unavailable). Run dotnet restore .\Pharmacy.sln; dotnet build .\Pharmacy.sln. Test barcode lookup, medicine controls, recall in Swagger for both matching and cross-pharmacy/branch cases.
