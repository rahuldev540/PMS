# Pending Pharmacy Assignment Flow

This version separates account creation from pharmacy assignment.

## 1. Apply the database change

From the folder containing `PharmacyOnly.Api.csproj`:

```powershell
dotnet tool restore
dotnet ef database update
```

Migration applied: `20260811090000_AddPendingPharmacyAssignment`.

## 2. Pharmacy Admin flow

### Create the Admin (Super Admin token)

```http
POST /api/pharmacy-super-admin/admins
Authorization: Bearer {superAdminToken}
Content-Type: application/json
```

```json
{
  "name": "Branch Admin",
  "email": "admin@example.com",
  "mobileNumber": "9000000001",
  "temporaryPassword": "AdminTemp@123"
}
```

The new account has `AccountStatus = PendingPharmacyAssignment`, with null hospital and branch.

### Admin logs in before assignment

```http
POST /api/pharmacy-admin-auth/login
```

The response is `200 OK` with a restricted token and:

```json
{
  "accountStatus": "PendingPharmacyAssignment",
  "isPharmacyAssigned": false,
  "canAccessPharmacyOperations": false
}
```

The token can call only Admin authentication endpoints (assignment status, change password, and logout). Other API calls return `403`.

### Super Admin assigns the branch

```http
PUT /api/pharmacy-super-admin/admins/{adminId}/pharmacy-assignment
Authorization: Bearer {superAdminToken}
Content-Type: application/json
```

```json
{
  "hospitalId": 1,
  "branchId": 2
}
```

The hospital must exist, and the branch must be active and belong to that hospital. Only one active Pharmacy Admin is allowed per branch.

The Admin must log in again. The new token contains `HospitalId`, `BranchId`, `AccountStatus=Active`, and `IsPharmacyAssigned=true`.

## 3. Pharmacist flow

### Create the Pharmacist (assigned Admin token)

```http
POST /api/pharmacy-admin/pharmacists
Authorization: Bearer {adminToken}
Content-Type: application/json
```

```json
{
  "name": "Branch Pharmacist",
  "email": "pharmacist@example.com",
  "mobileNumber": "9000000002",
  "temporaryPassword": "PharmacistTemp@123",
  "permissions": []
}
```

The new Pharmacist is pending and can authenticate but cannot use pharmacy operations.

### Admin assigns the Pharmacist

```http
PUT /api/pharmacy-admin/pharmacists/{pharmacistId}/pharmacy-assignment
Authorization: Bearer {adminToken}
Content-Type: application/json
```

```json
{
  "hospitalId": 1,
  "branchId": 2
}
```

The supplied IDs must exactly equal the hospital and branch in the Admin's JWT. The Admin cannot assign a Pharmacist to another branch.

The Pharmacist must log in again after assignment.

## 4. Check assignment status

Admin:

```http
GET /api/pharmacy-admin-auth/assignment-status
```

Pharmacist:

```http
GET /api/pharmacy-auth/assignment-status
```

These endpoints read the latest database state. `requiresNewLogin=true` means assignment is complete but the current JWT is still the old restricted token.

## 5. Important behavior

- Users never assign a pharmacy to themselves.
- Super Admin remains global and needs no pharmacy assignment.
- A pending Admin cannot create Pharmacists or use inventory, billing, reports, or other operational APIs.
- A pending Pharmacist cannot use dispensing, inventory, billing, or reports.
- Assignment audit fields are stored on `Users`: `CreatedByUserId`, `PharmacyAssignedByUserId`, and `PharmacyAssignedAt`.
- Assignment endpoints perform the initial assignment once. A separate audited transfer feature should be used for future reassignment.
