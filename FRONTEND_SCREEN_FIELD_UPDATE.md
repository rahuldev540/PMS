# Frontend Screen Field Update

This update aligns the API request models and persisted entities with the fields visible in the supplied Pharmacy Management frontend screens.

## Medicines
Added/accepted: Medicine Name, Generic Name, Medicine Code (auto-generated when omitted), Category, Dosage Form, Strength, Unit, Manufacturer, Selling Price, Initial Stock Count, Minimum Stock Threshold, Reorder Stock Level, Prescription Required, Catalogue Status, Description / Usage Notes.

## Add Stock
Added/accepted: Medicine ID, Batch Number, Stock Quantity, Expiry Date, Cost Price, Selling Price, Supplier/Supplier ID, Purchase Date, Notes. Existing inventory-level/GST fields remain backward compatible.

## Suppliers
Added/accepted: Supplier Name, Contact Person, Phone Number, Email Address, GST / Tax ID, Status, City, State, Address, Notes. Supplier Code is now optional for the frontend and is generated when omitted.

## Purchase Orders
Added/accepted: Supplier Name/Supplier ID, Supplier Contact Phone, Medicine Name/Medicine ID, Batch Number, Order Quantity, Unit Price, Payment Status, Order Status. Existing multi-item purchase-order payloads remain supported.

## Stock Transfers
Added/accepted: From Location / Pharmacy, To Location / Pharmacy, Medicine Name/ID, Batch Number/ID, Transfer Quantity, Requested By, Approved By, Notes. Name fields can resolve IDs where possible.

## CMS Integration permission error
CMS integration endpoints now use the `CMS Integration` permission module. The settings read endpoint also permits `CMS Integration -> View` as a fallback because the CMS screen reads operation-mode/link information from it. This prevents the repeated `You do not have View permission for Settings` error shown in the supplied screen when CMS Integration access is granted separately.

## Database
Migration `20260826154500_AddFrontendScreenFields` adds the persisted columns needed by the new screen fields.


## Prescriptions, Dispensing and Expiry Alerts completion

The following frontend screen contracts are now covered by the backend:

- **Prescriptions** (`GET /api/pharmacy/prescriptions`)
  - `PrescriptionId`, `PatientName`, `Doctor`, `PrescriptionDate`, `Medicines`, `Items`, `Status`
  - summary counters: `TotalPrescriptions`, `Pending`, `InProgress`, `Completed`, `Cancelled`
  - search now includes prescription id, patient, doctor and medicine name
  - frontend status labels such as `In Progress` are normalized to database workflow states
- **Dispensing** (`GET /api/pharmacy/dispensing`)
  - `Patient`, `PrescriptionId`, `Medicine`, `Medicines`, `Quantity`, `InvoiceId`, `Amount`, `PaymentStatus`, `DispensingStatus`, `Date`
  - summary counters: `PendingPrescriptions`, `TodaysDispensing`, `PendingBills`, `PaidBills`, `TodaysRevenue`
  - supports `search`, `paymentStatus` and `dispensingStatus` query filters
  - today's dispensing counts distinct prescriptions, not individual stock transaction lines
- **Expiry Alerts**
  - `GET /api/inventory/near-expiry` now includes `BatchId`, `BatchNumber`, `Quantity`, `ExpiryDate`, `DaysLeft`, `Status` in addition to inventory/medicine fields
  - `GET /api/inventory/near-expiry-details?days=30` and `GET /api/inventory/expired` expose frontend-friendly `Quantity`, `DaysLeft` and `Status` while retaining `RemainingQuantity` for compatibility
  - near-expiry no longer mixes already-expired batches into the near-expiry list

No database migration is required for these three screen-contract changes because they use existing prescription, billing, stock transaction and medicine batch columns.
