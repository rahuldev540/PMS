# Running EF Core migrations

The project includes an `IDesignTimeDbContextFactory<AppDbContext>`, an explicit
assembly identity, a Visual Studio solution, and a local `dotnet-ef` tool
manifest. These remove the Package Manager Console assembly-discovery problem.

## Recommended method

Open PowerShell in the directory containing `PharmacyOnly.Api.csproj` and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\create-migration.ps1
```

The script restores the exact EF tool version, builds the project, creates
`Migrations/InitialCreate` when no migration exists, and updates the database.

## Visual Studio Package Manager Console

Open `PharmacyOnly.Api.sln`, set `PharmacyOnly.Api` as the startup and default
project, then enter this as one line:

```powershell
Add-Migration InitialCreate -Project PharmacyOnly.Api -StartupProject PharmacyOnly.Api -Context AuthDemo.Data.AppDbContext -OutputDir Migrations -Verbose
```

Then run:

```powershell
Update-Database -Project PharmacyOnly.Api -StartupProject PharmacyOnly.Api -Context AuthDemo.Data.AppDbContext -Verbose
```

If the target database already contains the original CMS/pharmacy tables, do
not create a second `InitialCreate`. Use the provided database upgrade SQL or
create a baseline migration appropriate to that existing database.
