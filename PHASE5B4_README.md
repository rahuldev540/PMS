# Phase 5B4 — Realtime branch isolation

Changes:
- `PharmacyRealtimeNotifier`: publishes only to a pharmacy+branch SignalR group instead of `Clients.All`; payload now has `PharmacyId` rather than `HospitalId`.
- `PharmacyHub`: joins an authenticated pharmacist with valid `PharmacyId` and `BranchId` token claims to the corresponding group. No arbitrary client-selected group joins.
- `PharmacyService`: sends inventory and prescription events by `pharmacyId`, independent of legacy `HospitalId`.

Compatibility: This is a **behavior change** for SignalR clients. Admin clients with no BranchId claim will not receive branch events yet; a future phase must implement authorized all-branch subscriptions. Clients must use `PharmacyId` in event envelopes instead of `HospitalId`.

No entity/schema changes; no migration. Remaining HospitalId/CMS references still exist. ZIP integrity checked, not compiled (dotnet SDK unavailable). Run `dotnet build .\Pharmacy.sln` and test SignalR with two pharmacies and two branches.
