# Phase 12 – targeted backend login and slow API diagnostics

Base: PMS_STANDALONE_PHASE11_PHARMACIST_PERMISSION_ID_FIX(2).zip

## Changed files
- Controllers/UnifiedAuthController.cs: login user lookup uses AsNoTracking; login audit save is bounded to a five-second SQL command timeout and database audit failures are logged without rejecting an otherwise authenticated login. Adds successful login timing and trace ID logging. Existing response shapes and login route remain unchanged.
- Program.cs: logs API requests taking >=1000ms (method, route path, status, elapsed time, trace ID). No query strings, passwords, tokens or request bodies are logged by this middleware.

## Not changed
- Existing authentication roles and all-true default Pharmacist permissions.
- Phase 11 public Pharmacist ID to internal User ID permission mapping fix.
- JWT expiry, login request/response schema, and API routes.
- No migration, new APIs or database reset.

## Important limitations
- This is a backend-only change; it cannot fix frontend render delays, duplicate React requests, ngrok 502 errors or frontend timeout configuration.
- The five-second audit SQL timeout may add up to five seconds to an otherwise successful login during audit database failure. For production, consider a durable audit queue/outbox rather than best-effort logging.
- Login and API timing logs are diagnostics, not proof that any specific performance bottleneck is fixed.
- No .NET SDK is installed in the creation environment, so the build has NOT been executed here.

## Windows PowerShell instructions
1. Back up project and SQL database; extract ZIP to a new folder.
2. Check appsettings connection string (do not share secrets).
3. Run: dotnet restore .\Pharmacy.sln
4. Run: dotnet build .\Pharmacy.sln
5. Run: dotnet run --project .\PharmacyOnly.Api.csproj
6. Open local Swagger and POST /api/auth/login for each role. Test immediately and after restart.
7. Watch terminal for `Slow API` warnings; identify exact slow endpoints before optimizing SQL or adding pagination.
8. Compare the same login via ngrok Swagger and frontend; inspect Network status and request duration.

## Testing
- Correct password -> HTTP 200 with same response contract.
- Incorrect password -> HTTP 401.
- Inactive/invalid assignment -> HTTP 403 according to existing logic.
- Login audit DB error should be logged with trace ID, not fail an otherwise valid login.
- For slow pages, record the 3 slowest /api paths and frontend Network waterfall; frontend bundle/render performance requires frontend source.
