# Pharmacy API catalog

## Pharmacy role flow

- `PharmacySuperAdmin` is global and creates one `PharmacyAdmin` per hospital branch.
- `PharmacyAdmin` is branch-scoped and manages pharmacists, inventory, suppliers, purchases and reports.
- `Pharmacist` is branch-scoped and dispenses completed prescriptions to patients.
- The CMS backend uses `X-Integration-Key` to read branch medicine availability and submit doctor prescriptions.

### Pharmacy Super Admin

- `POST /api/pharmacy-super-admin-auth/login`
- `POST /api/pharmacy-super-admin-auth/change-password`
- `POST /api/pharmacy-super-admin-auth/logout`
- `GET /api/pharmacy-super-admin/hospitals`
- `GET /api/pharmacy-super-admin/hospitals/{hospitalId}/branches`
- `POST /api/pharmacy-super-admin/admins`
- `GET /api/pharmacy-super-admin/admins`
- `GET /api/pharmacy-super-admin/admins/{id}`
- `PUT /api/pharmacy-super-admin/admins/{id}`
- `PATCH /api/pharmacy-super-admin/admins/{id}/status`
- `POST /api/pharmacy-super-admin/admins/{id}/reset-password`

### CMS doctor integration

- `GET /api/integration/medicines?hospitalId={hospitalId}&branchId={branchId}&search={text}`
- `POST /api/integration/prescriptions`

Branch-protected routes require a Pharmacy JWT containing `HospitalId`, `BranchId`, user id and role claims. The global Pharmacy Super Admin JWT intentionally has no hospital or branch claim. CMS JWTs are not accepted; the CMS backend uses the integration endpoints and a separate integration key.

## Authentication and pharmacist administration

- `POST /api/pharmacy-auth/login`
- `POST /api/pharmacy-auth/forgot-password`
- `POST /api/pharmacy-auth/verify-reset-otp`
- `POST /api/pharmacy-auth/reset-password`
- `POST /api/pharmacy-auth/change-password`
- `POST /api/pharmacy-auth/logout`
- `POST /api/admin/pharmacists`
- `GET /api/admin/pharmacists`
- `GET /api/admin/pharmacists/{id}`
- `PUT /api/admin/pharmacists/{id}`
- `PATCH /api/admin/pharmacists/{id}/status`
- `PUT /api/admin/pharmacists/{id}/permissions`
- `POST /api/admin/pharmacists/{id}/reset-password`

## Medicine master and doctor access

- `POST /api/medicine`
- `GET /api/medicine`
- `GET /api/medicine/{id}`
- `PUT /api/medicine/{id}`
- `DELETE /api/medicine/{id}`
- `GET /api/medicine/search?keyword=`
- `GET /api/medicine/available`
- `GET /api/medicine/categories`
- `GET /api/medicine/dosage-forms`
- `GET /api/medicine/strengths`

## Doctor prescriptions

- `POST /api/doctor/prescriptions`
- `GET /api/doctor/prescriptions/{id}`
- `PUT /api/doctor/prescriptions/{id}`
- `POST /api/doctor/prescriptions/{id}/complete`
- `POST /api/doctor/prescriptions/{id}/cancel`

## Suppliers and purchasing

- `POST /api/suppliers`
- `GET /api/suppliers`
- `GET /api/suppliers/{id}`
- `PUT /api/suppliers/{id}`
- `PATCH /api/suppliers/{id}/status`
- `GET /api/suppliers/{id}/purchase-history`
- `POST /api/purchase-orders`
- `GET /api/purchase-orders`
- `GET /api/purchase-orders/pending`
- `GET /api/purchase-orders/{id}`
- `PATCH /api/purchase-orders/{id}/status`
- `POST /api/purchase-orders/{id}/receive`

## Inventory, batches and stock ledger

- `POST /api/inventory/add-stock`
- `GET /api/inventory`
- `GET /api/inventory/{medicineId}`
- `GET /api/inventory/low-stock`
- `GET /api/inventory/out-of-stock`
- `GET /api/inventory/near-expiry`
- `GET /api/inventory/near-expiry-details?days=30`
- `GET /api/inventory/stock-details?search=&stockStatus=&expiryStatus=&pageNumber=1&pageSize=25` (combined medicine, inventory, batch and supplier view)
- `GET /api/inventory/expired`
- `GET /api/inventory/{medicineId}/batches`
- `GET /api/inventory/batches/{batchId}`
- `PUT /api/inventory/{medicineId}/levels`
- `GET /api/inventory/transactions`
- `POST /api/inventory/adjustments`
- `POST /api/inventory/batches/{batchId}/dispose`
- `PATCH /api/inventory/batches/{batchId}/quarantine`
- `GET /api/inventory/summary`
- `GET /api/inventory/valuation`

## Branch transfers

- `POST /api/inventory/transfers`
- `GET /api/inventory/transfers`
- `GET /api/inventory/transfers/{id}`
- `POST /api/inventory/transfers/{id}/status`
- `POST /api/inventory/transfers/{id}/dispatch`
- `POST /api/inventory/transfers/{id}/receive`

## Prescriptions, dispensing, bills and returns

- `GET /api/pharmacy/pending`
- `GET /api/pharmacy/prescriptions/{id}`
- `POST /api/pharmacy/dispense` (FEFO batch deduction)
- `POST /api/pharmacy/generate-bill`
- `POST /api/pharmacy/payment`
- `GET /api/pharmacy/invoice/{billId}`
- `GET /api/pharmacy/bills`
- `GET /api/pharmacy/bills/{id}`
- `POST /api/pharmacy/bills/{id}/cancel`
- `GET /api/pharmacy/payments`
- `POST /api/pharmacy/payments/{id}/refund`
- `POST /api/pharmacy/returns`
- `GET /api/pharmacy/returns`
- `GET /api/pharmacy/returns/{id}`
- `POST /api/pharmacy/returns/{id}/approve`
- `POST /api/pharmacy/returns/{id}/cancel`

## Dashboard, reports and audit

- `GET /api/pharmacy/dashboard`
- `GET /api/pharmacy/alerts`
- `GET /api/pharmacy/reports/daily-sales?date=`
- `GET /api/pharmacy/reports/sales?from=&to=`
- `GET /api/pharmacy/reports/stock-summary`
- `GET /api/pharmacy/reports/stock-movement?from=&to=`
- `GET /api/pharmacy/reports/expiry?days=30`
- `GET /api/pharmacy/reports/purchases?from=&to=`
- `GET /api/pharmacy/reports/top-selling?from=&to=`
- `GET /api/pharmacy/audit-logs`

## Installation

1. Back up the shared database.
2. Run `Database/20260804_required_pharmacy_modules.sql` once.
3. Configure `ConnectionStrings:DefaultConnection` and `Jwt:Key`.
4. Run `dotnet restore`, `dotnet build`, and `dotnet run`.
5. Open Swagger and authorize with a suitable JWT.
