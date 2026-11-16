# Swagger: how to find the correct API

No API URL was renamed. Existing clients and Postman requests remain compatible.

Swagger is now arranged in numbered business-flow sections:

1. Super Admin sign-in
2. Super Admin creates and manages Pharmacy Admins
3. Pharmacy Admin sign-in
4. Pharmacy Admin creates and manages Pharmacists
5. Pharmacist sign-in and forgot/reset password
6–11. Medicines, inventory, suppliers and purchase orders
12–13. CMS integration and doctor prescriptions
14–18. Dispensing, bills, returns, transfers and reports

## How to test

1. Run the API and open `/swagger`.
2. Expand only the numbered section you need.
3. Read the plain-language operation title and **Access** line.
4. For protected APIs, click **Authorize** and paste only the JWT.
5. Click **Try it out**, fill the request, and click **Execute**.
6. For CMS integration, provide `X-Integration-Key`; do not use a Pharmacy JWT.

## Authentication routes

| User | Sign in | Change password | Forgot/reset password |
|---|---|---|---|
| Pharmacy Super Admin | `POST /api/pharmacy-super-admin-auth/login` | `POST /api/pharmacy-super-admin-auth/change-password` | Not implemented |
| Pharmacy Admin | `POST /api/pharmacy-admin-auth/login` | `POST /api/pharmacy-admin-auth/change-password` | Not implemented |
| Pharmacist | `POST /api/pharmacy-auth/login` | `POST /api/pharmacy-auth/change-password` | `forgot-password` → `verify-reset-otp` → `reset-password` |

## Account-creation routes

| Action | Endpoint | Caller |
|---|---|---|
| Create Pharmacy Admin | `POST /api/pharmacy-super-admin/admins` | Pharmacy Super Admin |
| Create Pharmacist | `POST /api/pharmacy-admin/pharmacists` | Pharmacy Admin |

Admin forgot/reset-password routes are still not implemented. This Swagger clarity update changes documentation only, not authentication behavior.

## Required local secrets

The ZIP intentionally contains no working database, JWT, SMTP or integration credentials. Configure them before starting:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CONNECTION_STRING"
dotnet user-secrets set "PharmacyJwt:Key" "YOUR_RANDOM_SECRET_AT_LEAST_32_BYTES"
dotnet user-secrets set "EmailSettings:Email" "YOUR_SMTP_EMAIL"
dotnet user-secrets set "EmailSettings:Password" "YOUR_SMTP_APP_PASSWORD"
dotnet user-secrets set "Integration:ApiKey" "YOUR_CMS_INTEGRATION_KEY"
```
