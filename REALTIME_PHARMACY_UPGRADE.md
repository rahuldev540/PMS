# Real-time pharmacy upgrade

This upgrade is additive and keeps existing routes intact. Added: SignalR `/hubs/pharmacy`, FEFO/recall/quarantine-safe dispensing, cumulative partial dispensing, rowversion concurrency, CMS prescription stock reservation, automatic low-stock/near-expiry background alerts, barcode lookup, recalls, controlled-medicine flag, pharmacist shifts, supplier invoices and split payments.

## Migration
Run `Update-Database` (PMC) or `dotnet ef database update` after taking a DB backup. Migration: `20260915123000_AddRealtimePharmacyOperations`.

## New endpoints
- GET `/api/pharmacy/operations/barcode/{barcode}`
- POST `/api/pharmacy/operations/recalls`
- POST `/api/pharmacy/operations/shifts/open`
- GET `/api/pharmacy/operations/shifts/{id}`
- POST `/api/pharmacy/operations/shifts/{id}/close`
- POST `/api/pharmacy/operations/supplier-invoices`
- POST `/api/pharmacy/operations/bills/{billId}/split-payment`

## SignalR events
`NewPrescriptionReceived`, `InventoryChanged`, `PrescriptionUpdated`, `MedicineRecalled`, `BillPaymentUpdated`.

## Compatibility
Existing controller routes and existing DTO fields were not removed. New database fields have defaults or are nullable. Existing standalone/CMS mode behavior remains.
