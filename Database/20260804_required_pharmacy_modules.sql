/* Run against the shared SQL Server database after taking a backup. */
BEGIN TRANSACTION;

ALTER TABLE MedicineBatches ADD SupplierId int NULL, PurchaseOrderId int NULL, GoodsReceiptId int NULL, IsQuarantined bit NOT NULL CONSTRAINT DF_MedicineBatches_IsQuarantined DEFAULT(0);
ALTER TABLE StockTransactions ADD MedicineBatchId int NULL, ReferenceType nvarchar(50) NULL, ReferenceId int NULL, CreatedByUserId int NULL;

CREATE TABLE Suppliers (
 Id int IDENTITY PRIMARY KEY, HospitalId int NOT NULL, BranchId int NOT NULL, Code nvarchar(50) NOT NULL,
 Name nvarchar(200) NOT NULL, ContactPerson nvarchar(150) NOT NULL DEFAULT(''), Phone nvarchar(30) NOT NULL DEFAULT(''),
 Email nvarchar(200) NOT NULL DEFAULT(''), Address nvarchar(500) NOT NULL DEFAULT(''), TaxNumber nvarchar(100) NOT NULL DEFAULT(''),
 IsActive bit NOT NULL DEFAULT(1), CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT UQ_Suppliers UNIQUE(HospitalId, BranchId, Code)
);

CREATE TABLE PurchaseOrders (
 Id int IDENTITY PRIMARY KEY, HospitalId int NOT NULL, BranchId int NOT NULL, SupplierId int NOT NULL,
 OrderNumber nvarchar(100) NOT NULL, OrderDate datetime2 NOT NULL, ExpectedDate datetime2 NULL,
 Status nvarchar(40) NOT NULL, Notes nvarchar(1000) NOT NULL DEFAULT(''), CreatedByUserId int NOT NULL,
 CONSTRAINT FK_PurchaseOrders_Suppliers FOREIGN KEY(SupplierId) REFERENCES Suppliers(Id),
 CONSTRAINT UQ_PurchaseOrders UNIQUE(HospitalId, BranchId, OrderNumber)
);

CREATE TABLE PurchaseOrderItems (
 Id int IDENTITY PRIMARY KEY, PurchaseOrderId int NOT NULL, MedicineId int NOT NULL,
 OrderedQuantity int NOT NULL, ReceivedQuantity int NOT NULL DEFAULT(0), UnitCost decimal(18,2) NOT NULL,
 CONSTRAINT FK_PurchaseOrderItems_PO FOREIGN KEY(PurchaseOrderId) REFERENCES PurchaseOrders(Id) ON DELETE CASCADE,
 CONSTRAINT FK_PurchaseOrderItems_Medicine FOREIGN KEY(MedicineId) REFERENCES Medicines(Id)
);

CREATE TABLE GoodsReceipts (
 Id int IDENTITY PRIMARY KEY, HospitalId int NOT NULL, BranchId int NOT NULL, SupplierId int NOT NULL,
 PurchaseOrderId int NULL, ReceiptNumber nvarchar(100) NOT NULL, SupplierInvoiceNumber nvarchar(100) NOT NULL,
 ReceivedDate datetime2 NOT NULL, ReceivedByUserId int NOT NULL
);

CREATE TABLE PharmacyPayments (
 Id int IDENTITY PRIMARY KEY, HospitalId int NOT NULL, BranchId int NOT NULL, PharmacyBillId int NOT NULL,
 PaymentMode nvarchar(40) NOT NULL, TransactionId nvarchar(150) NULL, PaidAmount decimal(18,2) NOT NULL,
 RefundedAmount decimal(18,2) NOT NULL DEFAULT(0), Status nvarchar(40) NOT NULL, ReceivedByUserId int NOT NULL,
 PaymentDate datetime2 NOT NULL, CONSTRAINT FK_PharmacyPayments_Bills FOREIGN KEY(PharmacyBillId) REFERENCES PharmacyBills(Id)
);

CREATE TABLE StockAdjustments (
 Id int IDENTITY PRIMARY KEY, HospitalId int NOT NULL, BranchId int NOT NULL, MedicineId int NOT NULL,
 MedicineBatchId int NULL, AdjustmentType nvarchar(40) NOT NULL, Quantity int NOT NULL, Reason nvarchar(500) NOT NULL,
 CreatedByUserId int NOT NULL, CreatedAt datetime2 NOT NULL
);

CREATE TABLE PharmacyReturns (
 Id int IDENTITY PRIMARY KEY, HospitalId int NOT NULL, BranchId int NOT NULL, ReturnType nvarchar(40) NOT NULL,
 MedicineId int NOT NULL, MedicineBatchId int NULL, PharmacyBillId int NULL, SupplierId int NULL, Quantity int NOT NULL,
 RefundAmount decimal(18,2) NOT NULL, Reason nvarchar(500) NOT NULL, Disposition nvarchar(40) NOT NULL,
 Status nvarchar(40) NOT NULL, CreatedByUserId int NOT NULL, CreatedAt datetime2 NOT NULL
);

CREATE TABLE StockTransfers (
 Id int IDENTITY PRIMARY KEY, HospitalId int NOT NULL, FromBranchId int NOT NULL, ToBranchId int NOT NULL,
 MedicineId int NOT NULL, MedicineBatchId int NULL, Quantity int NOT NULL, Status nvarchar(40) NOT NULL,
 Notes nvarchar(500) NOT NULL, RequestedByUserId int NOT NULL, RequestedAt datetime2 NOT NULL, CompletedAt datetime2 NULL
);

CREATE TABLE PharmacyAlerts (
 Id int IDENTITY PRIMARY KEY, HospitalId int NOT NULL, BranchId int NOT NULL, AlertType nvarchar(40) NOT NULL,
 MedicineId int NULL, MedicineBatchId int NULL, Message nvarchar(500) NOT NULL, IsRead bit NOT NULL DEFAULT(0),
 IsResolved bit NOT NULL DEFAULT(0), CreatedAt datetime2 NOT NULL
);

CREATE INDEX IX_MedicineBatches_Expiry_Remaining ON MedicineBatches(ExpiryDate, RemainingQuantity);
CREATE INDEX IX_StockTransactions_Ledger ON StockTransactions(HospitalId, BranchId, MedicineId, TransactionDate);
CREATE INDEX IX_PharmacyPayments_Bill ON PharmacyPayments(PharmacyBillId);
CREATE INDEX IX_PharmacyReturns_Filter ON PharmacyReturns(HospitalId, BranchId, ReturnType, Status);
CREATE INDEX IX_StockTransfers_Filter ON StockTransfers(HospitalId, FromBranchId, ToBranchId, Status);

COMMIT TRANSACTION;
