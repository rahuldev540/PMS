# Phase 5B3 — Pharmacy operations tenant filtering

Updated `Controllers/RealtimePharmacyOperationsController.cs` to use `PharmacyId` + `BranchId` instead of legacy `HospitalId` for medicine recalls, shift opening/closing, supplier invoices, and split payments. Added tenant claim guards, supplier/purchase-order ownership checks, and scoped payment summation. Existing legacy database columns and model mappings remain intact for migration compatibility.

**No EF migration is required for this phase.** Do not drop `HospitalId` columns yet.

**Validation:** ZIP integrity checked. .NET SDK is unavailable in this environment, so build and Swagger integration tests must be run locally.

**Note:** Existing realtime notifier still uses a legacy hospital-named argument and broadcasts to all clients; realtime event audience isolation must be fixed in a later phase before production use.
