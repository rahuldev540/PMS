using AuthDemo.Constants;
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Helpers;

/// <summary>
/// Maintains USER-SPECIFIC pharmacy permissions.
///
/// IMPORTANT:
/// - PharmacyPermissionRole is no longer used.
/// - PharmacyRolePermission is no longer used.
/// - PharmacyPermissionRoleId is no longer used.
/// - UserPermissions is the only permission source.
/// - Existing permission values are preserved.
/// - Missing modules are created with TRUE by default.
/// - PharmacySuperAdmin does not require UserPermissions.
/// </summary>
public static class PharmacyPermissionSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        // Only active PharmacyAdmin / Pharmacist users need
        // user-specific permission rows.
        var users = await db.Users
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                (x.Role == UserRoles.PharmacyAdmin ||
                 x.Role == UserRoles.Pharmacist))
            .Select(x => new
            {
                x.Id,
                x.Role
            })
            .ToListAsync();

        if (users.Count == 0)
            return;

        foreach (var user in users)
        {
            var modules = GetModulesForRole(user.Role);

            if (modules.Length == 0)
                continue;

            var existingPermissions = await db.UserPermissions
                .Where(x => x.UserId == user.Id)
                .OrderBy(x => x.Id)
                .ToListAsync();

            // Normalize old/internal module names without changing
            // any View/Create/Edit/Delete values.
            foreach (var permission in existingPermissions)
            {
                var normalized = NormalizeModule(permission.Module);

                if (!string.IsNullOrWhiteSpace(normalized))
                    permission.Module = normalized;
            }

            // Merge duplicate rows that may result from old names.
            // If either duplicate granted an action, preserve it.
            var duplicateGroups = existingPermissions
                .Where(x => !string.IsNullOrWhiteSpace(x.Module))
                .GroupBy(
                    x => x.Module,
                    StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .ToList();

            foreach (var group in duplicateGroups)
            {
                var ordered = group
                    .OrderBy(x => x.Id)
                    .ToList();

                var primary = ordered[0];

                foreach (var duplicate in ordered.Skip(1))
                {
                    primary.CanView |= duplicate.CanView;
                    primary.CanCreate |= duplicate.CanCreate;
                    primary.CanEdit |= duplicate.CanEdit;
                    primary.CanDelete |= duplicate.CanDelete;

                    if (duplicate.UpdatedAt > primary.UpdatedAt)
                    {
                        primary.UpdatedAt = duplicate.UpdatedAt;
                        primary.AssignedByUserId =
                            duplicate.AssignedByUserId;
                    }

                    db.UserPermissions.Remove(duplicate);
                    existingPermissions.Remove(duplicate);
                }
            }

            // Save normalization/duplicate cleanup before adding
            // missing canonical modules. This avoids unique-index
            // conflicts on UserId + Module.
            await db.SaveChangesAsync();

            existingPermissions = await db.UserPermissions
                .Where(x => x.UserId == user.Id)
                .OrderBy(x => x.Id)
                .ToListAsync();

            var now = DateTime.UtcNow;

            foreach (var module in modules)
            {
                var canonicalModule = NormalizeModule(module);

                if (string.IsNullOrWhiteSpace(canonicalModule))
                    continue;

                var existing = existingPermissions
                    .FirstOrDefault(x =>
                        string.Equals(
                            NormalizeModule(x.Module),
                            canonicalModule,
                            StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    // Keep canonical spelling/casing only.
                    // Never overwrite permission flags.
                    existing.Module = canonicalModule;
                    continue;
                }

                var permission = new UserPermission
                {
                    UserId = user.Id,

                    // Standalone PMS permissions are not linked to hospitals.
                    // Keep the legacy database column nullable until schema cleanup.

                    Module = canonicalModule,

                    // Current PMS requirement:
                    // permissions are TRUE by default and can later
                    // be changed by Super Admin / Admin as allowed.
                    CanView = true,
                    CanCreate = true,
                    CanEdit = true,
                    CanDelete = true,

                    // Existing model uses a non-nullable int.
                    // 0 represents startup/system-created defaults.
                    AssignedByUserId = 0,

                    UpdatedAt = now
                };

                db.UserPermissions.Add(permission);
                existingPermissions.Add(permission);
            }

            await db.SaveChangesAsync();
        }
    }

    private static string[] GetModulesForRole(string? role)
    {
        if (string.Equals(
                role,
                UserRoles.PharmacyAdmin,
                StringComparison.OrdinalIgnoreCase))
        {
            return PermissionModules.PharmacyAdmin
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeModule)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (string.Equals(
                role,
                UserRoles.Pharmacist,
                StringComparison.OrdinalIgnoreCase))
        {
            return PermissionModules.Pharmacist
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeModule)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Converts legacy/internal module names to the canonical
    /// module names used by the current permission matrix.
    ///
    /// Keep this aligned with
    /// PharmacyPermissionChecker.ResolveAdminSidebarModule().
    /// </summary>
    private static string NormalizeModule(string? module)
    {
        if (string.IsNullOrWhiteSpace(module))
            return string.Empty;

        return module.Trim().ToLowerInvariant() switch
        {
            "dashboard" => "Dashboard",

            "branch" or
            "branches" => "Branches",

            "user" or
            "users" or
            "user management" or
            "users & permissions" or
            "users and permissions" => "Users",

            "medicine" or
            "medicines" => "Medicines",

            "inventory" or
            "stock" => "Stock",

            "supplier" or
            "suppliers" => "Suppliers",

            "purchase" or
            "purchases" or
            "purchase order" or
            "purchase orders" => "Purchase Orders",

            "stock transfer" or
            "stock transfers" => "Stock Transfers",

            "prescription" or
            "prescriptions" => "Prescriptions",

            "pending" => "Pending",

            "sale" or
            "sales" or
            "dispensing" or
            "billing" => "Dispensing",

            "bill" or
            "bills" => "Bills",

            "return" or
            "returns" => "Returns",

            "expiry" or
            "expiry alert" or
            "expiry alerts" => "Expiry Alerts",

            "report" or
            "reports" => "Reports",

            "setting" or
            "settings" or
            "system settings" => "Settings",

            _ => module.Trim()
        };
    }
}

