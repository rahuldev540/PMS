using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Data;

public static class PharmacySuperAdminSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine(">>> PHARMACY SUPER ADMIN SEEDER STARTED");
        Console.WriteLine("==============================================");

        try
        {
            // =====================================================
            // READ BOOTSTRAP CONFIGURATION
            // =====================================================

            var section = configuration.GetSection(
                "BootstrapPharmacySuperAdmin");

            var enabled = section.GetValue<bool>("Enabled");

            Console.WriteLine($">>> Bootstrap Enabled : {enabled}");
            Console.WriteLine($">>> Bootstrap Name    : {section["Name"]}");
            Console.WriteLine($">>> Bootstrap Email   : {section["Email"]}");
            Console.WriteLine($">>> Bootstrap Mobile  : {section["MobileNumber"]}");

            // Never print password to console.

            if (!enabled)
            {
                Console.WriteLine(
                    ">>> Seeder stopped because BootstrapPharmacySuperAdmin:Enabled is FALSE.");

                Console.WriteLine("==============================================");
                return;
            }

            // =====================================================
            // CREATE SERVICE SCOPE
            // =====================================================

            Console.WriteLine(">>> Creating database service scope...");

            using var scope = services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            Console.WriteLine(">>> AppDbContext resolved successfully.");

            // =====================================================
            // SHOW DATABASE CONNECTION
            // =====================================================

            var connection = db.Database.GetDbConnection();

            Console.WriteLine(
                $">>> Database Server   : {connection.DataSource}");

            Console.WriteLine(
                $">>> Database Name     : {connection.Database}");

            // =====================================================
            // CHECK DATABASE CONNECTION
            // =====================================================

            var canConnect = await db.Database.CanConnectAsync();

            Console.WriteLine(
                $">>> Database Connected: {canConnect}");

            if (!canConnect)
            {
                throw new InvalidOperationException(
                    "Unable to connect to the PMS database while seeding Pharmacy Super Admin.");
            }

            // =====================================================
            // CHECK WHETHER SUPER ADMIN ALREADY EXISTS
            // =====================================================

            Console.WriteLine(
                ">>> Checking for existing PharmacySuperAdmin...");

            var exists = await db.Users.AnyAsync(
                x => x.Role == "PharmacySuperAdmin");

            Console.WriteLine(
                $">>> Existing PharmacySuperAdmin: {exists}");

            if (exists)
            {
                Console.WriteLine(
                    ">>> Pharmacy Super Admin already exists. No new account created.");

                Console.WriteLine("==============================================");
                return;
            }

            // =====================================================
            // READ ADMIN DETAILS
            // =====================================================

            var name = section["Name"]?.Trim();

            var email = section["Email"]?
                .Trim()
                .ToLowerInvariant();

            var mobile = section["MobileNumber"]?.Trim();

            var password = section["Password"];

            // =====================================================
            // VALIDATE CONFIGURATION
            // =====================================================

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException(
                    "Bootstrap Pharmacy Super Admin Name is missing.");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new InvalidOperationException(
                    "Bootstrap Pharmacy Super Admin Email is missing.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "Bootstrap Pharmacy Super Admin Password is missing.");
            }

            if (password.Length < 8)
            {
                throw new InvalidOperationException(
                    "Super Admin password must contain at least 8 characters.");
            }

            // =====================================================
            // CHECK WHETHER EMAIL ALREADY EXISTS
            // =====================================================

            Console.WriteLine(
                $">>> Checking whether email already exists: {email}");

            var emailExists = await db.Users.AnyAsync(
                x =>
                    x.Email != null &&
                    x.Email.ToLower() == email);

            Console.WriteLine(
                $">>> Existing Email: {emailExists}");

            if (emailExists)
            {
                throw new InvalidOperationException(
                    "Super Admin email already belongs to another user.");
            }

            // =====================================================
            // CREATE SUPER ADMIN
            // =====================================================

            Console.WriteLine(
                ">>> Creating Pharmacy Super Admin...");

            var passwordHash =
                BCrypt.Net.BCrypt.HashPassword(password);

            var superAdmin = new User
            {
                Name = name,

                Email = email,

                MobileNumber = mobile,

                PasswordHash = passwordHash,

                Role = "PharmacySuperAdmin",

                // Super Admin is global and is not assigned
                // to a particular hospital or branch.

                BranchId = null,

                IsActive = true,

                // Super Admin can login immediately.
                MustChangePassword = false,

                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(superAdmin);

            // =====================================================
            // SAVE
            // =====================================================

            Console.WriteLine(
                ">>> Saving Pharmacy Super Admin to database...");

            var affectedRows = await db.SaveChangesAsync();

            Console.WriteLine(
                $">>> SaveChanges affected rows: {affectedRows}");

            // =====================================================
            // VERIFY INSERT
            // =====================================================

            var created = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Role == "PharmacySuperAdmin" &&
                        x.Email == email);

            if (created == null)
            {
                throw new InvalidOperationException(
                    "SaveChanges completed but Pharmacy Super Admin could not be found in Users table.");
            }

            Console.WriteLine();
            Console.WriteLine("==============================================");
            Console.WriteLine(">>> PHARMACY SUPER ADMIN CREATED SUCCESSFULLY");
            Console.WriteLine($">>> User Id : {created.Id}");
            Console.WriteLine($">>> Name    : {created.Name}");
            Console.WriteLine($">>> Email   : {created.Email}");
            Console.WriteLine($">>> Role    : {created.Role}");
            Console.WriteLine("==============================================");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("==============================================");
            Console.WriteLine(">>> PHARMACY SUPER ADMIN SEEDER FAILED");
            Console.WriteLine($">>> Exception Type: {ex.GetType().FullName}");
            Console.WriteLine($">>> Error         : {ex.Message}");

            if (ex.InnerException != null)
            {
                Console.WriteLine(
                    $">>> Inner Error   : {ex.InnerException.Message}");
            }

            Console.WriteLine("==============================================");
            Console.WriteLine();

            // Important: don't hide startup/seeding failures.
            throw;
        }
    }
}