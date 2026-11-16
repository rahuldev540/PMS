# Dashboard API Guide

All endpoints require a valid Bearer token for the stated role.

## Pharmacy Super Admin

Dashboard:

- `GET /api/pharmacy-super-admin/dashboard`

The response includes hospital/branch totals, Admin and Pharmacist totals,
pending assignments, recent Admins, and the available management operations.

Admin CRUD and lifecycle:

- `POST /api/pharmacy-super-admin/admins`
- `GET /api/pharmacy-super-admin/admins`
- `GET /api/pharmacy-super-admin/admins/{id}`
- `PUT /api/pharmacy-super-admin/admins/{id}`
- `DELETE /api/pharmacy-super-admin/admins/{id}` (soft delete)
- `PATCH /api/pharmacy-super-admin/admins/{id}/status`
- `PUT /api/pharmacy-super-admin/admins/{id}/pharmacy-assignment`
- `POST /api/pharmacy-super-admin/admins/{id}/reset-password`

Hospital/branch lookup:

- `GET /api/pharmacy-super-admin/hospitals`
- `GET /api/pharmacy-super-admin/hospitals/{hospitalId}/branches`

## Pharmacy Admin

Dashboard:

- `GET /api/pharmacy-admin/dashboard`

An unassigned Admin receives a pending dashboard and can create/view pending
Pharmacists. An assigned Admin receives branch-scoped totals, stock alerts,
sales totals and recent prescriptions.

Pharmacist CRUD and lifecycle:

- `POST /api/pharmacy-admin/pharmacists`
- `GET /api/pharmacy-admin/pharmacists`
- `GET /api/pharmacy-admin/pharmacists/{id}`
- `PUT /api/pharmacy-admin/pharmacists/{id}`
- `DELETE /api/pharmacy-admin/pharmacists/{id}` (soft delete)
- `PATCH /api/pharmacy-admin/pharmacists/{id}/status`
- `PUT /api/pharmacy-admin/pharmacists/{id}/permissions`
- `PUT /api/pharmacy-admin/pharmacists/{id}/pharmacy-assignment`
- `POST /api/pharmacy-admin/pharmacists/{id}/reset-password`

Branch-scoped management APIs:

- Medicines: `/api/medicine`
- Inventory: `/api/inventory`
- Suppliers: `/api/suppliers`
- Purchase orders: `/api/purchase-orders`
- Returns: `/api/pharmacy/returns`
- Stock transfers: `/api/inventory/transfers`
- Reports and alerts: `/api/pharmacy/reports/*`, `/api/pharmacy/alerts`

## Pharmacist

Dashboard:

- `GET /api/pharmacist/dashboard`

An unassigned Pharmacist receives assignment status only. An assigned
Pharmacist receives pending prescription totals, dispensing totals, medicine
availability, stock warnings and permission information.

Operational APIs:

- `GET /api/medicine/available`
- `GET /api/medicine/{id}`
- `GET /api/pharmacy/pending`
- `GET /api/pharmacy/prescriptions/{id}`
- `POST /api/pharmacy/dispense`
- `POST /api/pharmacy/generate-bill`
- `POST /api/pharmacy/payment`
- `GET /api/pharmacy/invoice/{billId}`
- `GET /api/pharmacy/reports/daily-sales`

## Assignment flow

1. Super Admin creates an unassigned Pharmacy Admin.
2. The Admin can log in and create/view pending Pharmacists.
3. Pharmacy operations return HTTP 403 while users are unassigned.
4. Super Admin assigns the Admin to a hospital and branch.
5. Admin logs in again and assigns Pharmacists to that same scope.
6. Pharmacists log in again to receive new scoped tokens.
7. Dashboard and operational data are restricted to the assigned branch.

Delete operations are soft deletes. They set `IsActive = false` so historical
prescriptions, bills, inventory transactions and audit data remain available.
