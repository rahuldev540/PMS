using AuthDemo.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Helpers;

public class PharmacyPermissionChecker
{
    private readonly AppDbContext _db;

    public PharmacyPermissionChecker(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Checks permission directly from UserPermissions.
    ///
    /// PharmacyPermissionRoles and PharmacyRolePermissions
    /// are NOT used.
    ///
    /// PharmacyAdmin and Pharmacist permissions are
    /// completely user-specific.
    /// </summary>
    public async Task<bool> HasPermissionAsync(
        int userId,
        string module,
        string action)
    {
        if (userId <= 0)
            return false;

        if (string.IsNullOrWhiteSpace(module))
            return false;

        if (string.IsNullOrWhiteSpace(action))
            return false;

        var requestedModule = ResolveAdminSidebarModule(module);

        if (string.IsNullOrWhiteSpace(requestedModule))
            return false;

        // ---------------------------------------------------------
        // 1. Load user
        // ---------------------------------------------------------

        var user = await _db.Users
            .AsNoTracking()
            .Where(x =>
                x.Id == userId &&
                x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Role,
                x.AccountStatus,
                x.PharmacyId,
                x.BranchId
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return false;

        // ---------------------------------------------------------
        // 2. Super Admin
        // ---------------------------------------------------------

        // Super Admin does not require UserPermissions.
        if (string.Equals(
                user.Role,
                "PharmacySuperAdmin",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // ---------------------------------------------------------
        // 3. Only PharmacyAdmin / Pharmacist
        // ---------------------------------------------------------

        var isPharmacyAdmin = string.Equals(
            user.Role,
            "PharmacyAdmin",
            StringComparison.OrdinalIgnoreCase);

        var isPharmacist = string.Equals(
            user.Role,
            "Pharmacist",
            StringComparison.OrdinalIgnoreCase);

        if (!isPharmacyAdmin && !isPharmacist)
            return false;

        // ---------------------------------------------------------
        // 4. Account must be active
        // ---------------------------------------------------------

        if (!string.Equals(
                user.AccountStatus,
                "Active",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // ---------------------------------------------------------
        // 5. Pharmacy assignment required
        // ---------------------------------------------------------

        if (!user.PharmacyId.HasValue)
            return false;

        // ---------------------------------------------------------
        // 6. Pharmacy must exist and be active
        // ---------------------------------------------------------

        var pharmacy = await _db.Pharmacies
            .AsNoTracking()
            .Where(x =>
                x.Id == user.PharmacyId.Value &&
                x.IsActive)
            .Select(x => new
            {
                x.Id
            })
            .FirstOrDefaultAsync();

        if (pharmacy == null)
            return false;

        // Standalone pharmacy: branch assignment is mandatory.
        if (!user.BranchId.HasValue)
            return false;

        // ---------------------------------------------------------
        // 8. Get USER-SPECIFIC permissions
        // ---------------------------------------------------------

        var permissions = await _db.UserPermissions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new
            {
                x.Module,
                x.CanView,
                x.CanCreate,
                x.CanEdit,
                x.CanDelete
            })
            .ToListAsync();

        // ---------------------------------------------------------
        // 9. Match normalized module
        // ---------------------------------------------------------

        var matchingPermissions = permissions
            .Where(x =>
                string.Equals(
                    ResolveAdminSidebarModule(x.Module),
                    requestedModule,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        // IMPORTANT:
        // Missing permission means NO ACCESS.
        //
        // Default TRUE permissions should be created when
        // PharmacyAdmin / Pharmacist is created.
        // ---------------------------------------------------------

        if (matchingPermissions.Count == 0)
            return false;

        // ---------------------------------------------------------
        // 10. Check requested action
        // ---------------------------------------------------------

        var requestedAction =
            action.Trim().ToLowerInvariant();

        return requestedAction switch
        {
            "view" =>
                matchingPermissions.Any(x => x.CanView),

            "create" =>
                matchingPermissions.Any(x => x.CanCreate),

            "edit" =>
                matchingPermissions.Any(x => x.CanEdit),

            "delete" =>
                matchingPermissions.Any(x => x.CanDelete),

            _ => false
        };
    }

    /// <summary>
    /// Normalizes API/frontend module names to the
    /// module names stored in UserPermissions.
    /// </summary>
    public static string ResolveAdminSidebarModule(
        string? module)
    {
        if (string.IsNullOrWhiteSpace(module))
            return string.Empty;

        return module
            .Trim()
            .ToLowerInvariant() switch
        {
            // Dashboard
            "dashboard"
                => "Dashboard",

            // Branches
            "branch" or
            "branches"
                => "Branches",

            // Users
            "user" or
            "users" or
            "user management" or
            "users & permissions" or
            "users and permissions"
                => "Users",

            // Medicines
            "medicine" or
            "medicines"
                => "Medicines",

            // Stock / Inventory
            "inventory" or
            "stock"
                => "Stock",

            // Suppliers
            "supplier" or
            "suppliers"
                => "Suppliers",

            // Purchase Orders
            "purchase" or
            "purchases" or
            "purchase order" or
            "purchase orders"
                => "Purchase Orders",

            // Stock Transfers
            "stock transfer" or
            "stock transfers"
                => "Stock Transfers",

            // Prescriptions
            "prescription" or
            "prescriptions"
                => "Prescriptions",

            // Pending
            "pending"
                => "Pending",

            // Dispensing
            "dispensing" or
            "sales"
                => "Dispensing",

            // Bills
            "bill" or
            "bills" or
            "billing"
                => "Bills",

            // Returns
            "return" or
            "returns"
                => "Returns",

            // Expiry Alerts
            "expiry" or
            "expiry alert" or
            "expiry alerts"
                => "Expiry Alerts",

            // Reports
            "report" or
            "reports"
                => "Reports",

            // Settings
            "setting" or
            "settings"
                => "Settings",

            // If exact module is already being used,
            // preserve it.
            _ => module.Trim()
        };
    }

    /// <summary>
    /// Maps Pharmacist permissions to the corresponding
    /// PharmacyAdmin permission.
    ///
    /// Used when PharmacyAdmin creates/updates a Pharmacist
    /// and tries to grant permissions.
    ///
    /// Admin cannot grant a permission that the Admin
    /// does not have.
    /// </summary>
    public static string ParentAdminModuleForPharmacist(
        string? module)
    {
        if (string.IsNullOrWhiteSpace(module))
            return string.Empty;

        var resolved =
            ResolveAdminSidebarModule(module);

        return resolved switch
        {
            // Pharmacist Pending prescriptions are controlled
            // by Admin Prescriptions permission.
            "Pending"
                => "Prescriptions",

            // Pharmacist dispensing
            "Dispensing"
                => "Dispensing",

            // Pharmacist Bills requires Admin Bills.
            "Bills"
                => "Bills",

            // Pharmacist Returns requires Admin Returns.
            "Returns"
                => "Returns",

            "Dashboard"
                => "Dashboard",

            "Medicines"
                => "Medicines",

            "Stock"
                => "Stock",

            "Reports"
                => "Reports",

            _ => resolved
        };
    }
}