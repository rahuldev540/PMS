# Phase 5B7 — Controller Legacy Initializer Cleanup

Removed seven explicit `HospitalId = null` object initializers from PharmacySuperAdminController, PharmacyAdminSettingsController, and PharmacyController. Nullable HospitalId remains implicitly null for newly constructed records. No entity, EF mapping, or database schema changed.

The pharmacist reassignment logic that explicitly clears HospitalId was intentionally retained to preserve legacy-record behavior. The Super Admin medicine list still supports hospitalId filtering and requires separate API contract review. This is NOT a complete removal of CMS/HospitalId.

No migration required. Build and test locally.
