namespace AuthDemo.Constants;

public static class PermissionModules
{
    /// <summary>
    /// All Pharmacy PMS permission modules.
    ///
    /// IMPORTANT:
    /// UserPermissionService creates these modules with:
    /// CanView   = true
    /// CanCreate = true
    /// CanEdit   = true
    /// CanDelete = true
    ///
    /// These are only DEFAULT values.
    /// They can later be changed from frontend/backend.
    /// </summary>
    public static readonly string[] All =
    {
        "Dashboard",
        "Branches",
        "Users",
        "Medicines",
        "Stock",
        "Suppliers",
        "Purchase Orders",
        "Stock Transfers",
        "Prescriptions",
        "Pending",
        "Dispensing",
        "Bills",
        "Returns",
        "Expiry Alerts",
        "Reports",
        "Settings"
    };


    // ============================================================
    // PHARMACY ADMIN
    // ============================================================
    //
    // Pharmacy Admin receives ALL modules by default.
    //
    // All permissions:
    // View/Create/Edit/Delete = TRUE
    //
    // Individual permissions can later be disabled.
    // ============================================================

    public static readonly string[] PharmacyAdmin =
    {
        "Dashboard",
        "Branches",
        "Users",
        "Medicines",
        "Stock",
        "Suppliers",
        "Purchase Orders",
        "Stock Transfers",
        "Prescriptions",
        "Pending",
        "Dispensing",
        "Bills",
        "Returns",
        "Expiry Alerts",
        "Reports",
        "Settings"
    };


    // ============================================================
    // PHARMACIST
    // ============================================================
    //
    // Pharmacist also receives ALL modules by default.
    //
    // All permissions:
    // View/Create/Edit/Delete = TRUE
    //
    // Individual permissions can later be disabled.
    // ============================================================

    public static readonly string[] Pharmacist =
    {
        "Dashboard",
        "Users",
        "Medicines",
        "Stock",
        "Suppliers",
        "Purchase Orders",
        "Stock Transfers",
        "Prescriptions",
        "Pending",
        "Dispensing",
        "Bills",
        "Returns",
        "Expiry Alerts",
        "Reports",
        "Settings"
    };


    /// <summary>
    /// Returns permission modules based on role.
    /// </summary>
    public static string[] GetModulesForRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return Array.Empty<string>();
        }

        if (role.Equals(
            UserRoles.PharmacyAdmin,
            StringComparison.OrdinalIgnoreCase))
        {
            return PharmacyAdmin;
        }

        if (role.Equals(
            UserRoles.Pharmacist,
            StringComparison.OrdinalIgnoreCase))
        {
            return Pharmacist;
        }

        return Array.Empty<string>();
    }


    /// <summary>
    /// Checks whether a module is a valid Pharmacy PMS module.
    /// </summary>
    public static bool IsValidModule(string module)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            return false;
        }

        return All.Contains(
            module,
            StringComparer.OrdinalIgnoreCase);
    }
}