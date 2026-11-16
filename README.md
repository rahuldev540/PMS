# Pharmacy Only API

The pharmacy application has three separate local roles:

1. `PharmacySuperAdmin` manages all branch pharmacies and creates branch admins.
2. `PharmacyAdmin` manages one hospital branch, its pharmacists and inventory.
3. `Pharmacist` dispenses completed prescriptions to patients in the assigned branch.

The CMS remains the source of hospitals, branches, doctors, patients and appointments in the shared database. The CMS backend reads available medicines and submits completed prescriptions through `/api/integration`, using the `X-Integration-Key` header.

## Required secrets

Configure these with .NET user secrets or environment variables before running:

```powershell
dotnet user-secrets set "PharmacyJwt:Key" "A-NEW-RANDOM-SECRET-OF-AT-LEAST-32-BYTES"
dotnet user-secrets set "Integration:ApiKey" "A-SEPARATE-RANDOM-CMS-INTEGRATION-SECRET"
dotnet user-secrets set "EmailSettings:Email" "sender@example.com"
dotnet user-secrets set "EmailSettings:Password" "EMAIL-APP-PASSWORD"
```

## First Pharmacy Super Admin

Temporarily add the following through user secrets (preferred) or development configuration:

```powershell
dotnet user-secrets set "BootstrapPharmacySuperAdmin:Enabled" "true"
dotnet user-secrets set "BootstrapPharmacySuperAdmin:Name" "Pharmacy Super Administrator"
dotnet user-secrets set "BootstrapPharmacySuperAdmin:Email" "superadmin@example.com"
dotnet user-secrets set "BootstrapPharmacySuperAdmin:MobileNumber" "9876543210"
dotnet user-secrets set "BootstrapPharmacySuperAdmin:Password" "TemporarySuperAdmin@123"
```

Start the API once, log in through `POST /api/pharmacy-super-admin-auth/login`, change the password, and then disable bootstrap:

```powershell
dotnet user-secrets set "BootstrapPharmacySuperAdmin:Enabled" "false"
dotnet user-secrets remove "BootstrapPharmacySuperAdmin:Password"
```

See `API_CATALOG.md` for the endpoint list.

This package exposes the complete end-to-end Pharmacy-related HTTP APIs.

See `API_CATALOG.md` for the module-wise route catalog and `DELIVERY_NOTES.md`
for the implemented modules and deployment notes.

For EF Core setup and the assembly-loading fix, open `MIGRATION_SETUP.md` or run
`create-migration.ps1` from the project directory.

## Exposed controllers

- `PharmacyAuthController` — Pharmacist login
- `MedicineController` — Medicine master and search
- `InventoryController` — Stock, low stock, out of stock, near expiry
- `PharmacyController` — Prescriptions, dispensing, bills, payments, invoices and sales

The general CMS `AuthController` and every Doctor, Nurse, Receptionist,
Patient, Appointment, Laboratory, Billing, Dashboard, Report, Settings and
Administration controller have been removed from this package.

Only Pharmacy DTOs and Pharmacy-required models remain. Shared prescription,
patient, doctor, hospital, branch and appointment entities are included because
the dispensing workflow reads records created by the CMS API. No Laboratory or
other CMS DTOs, controllers, services, migrations, or models are included.

## Database

Configure `ConnectionStrings:DefaultConnection` in `appsettings.json`. Use the
same database as the CMS API when Pharmacy must dispense CMS prescriptions.

## Run

```bash
dotnet restore
dotnet build
dotnet run
```

Open Swagger to confirm that only Pharmacy authentication, Medicine,
Inventory and Pharmacy endpoints are shown.
