# One-step branch medicine import

POST /api/medicine-import/upload (PharmacyAdmin JWT), multipart/form-data: `File` (.xlsx or .csv), `BranchId` (pharmacy-scoped branch number). No separate commit request.

On successful upload, existing import service writes medicines, suppliers, medicine batches, inventory and stock transactions within a transaction. A file containing any invalid rows is rejected before committing any stock/medicine/supplier rows. StockQuantity, Stock Quantity and Quantity are accepted headers. Re-uploading the same batch uses the existing `Skip` duplicate behavior to prevent duplicate stock increases; new batches add stock.

The import status/staging record can remain for auditing even when a file is rejected. `LastPurchase` is not automatically treated as a purchase order or purchase history: importing an arbitrary date must not fabricate a purchase transaction.

Build and integration tests must be run in a .NET SDK environment with SQL Server; no .NET SDK was available in the packaging environment.
