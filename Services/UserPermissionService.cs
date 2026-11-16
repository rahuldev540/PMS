using AuthDemo.Constants;
using AuthDemo.Data;
using AuthDemo.Helpers;
using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

public class UserPermissionService
{
    private readonly AppDbContext _context;

    public UserPermissionService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates default USER-SPECIFIC permissions.
    ///
    /// RULES:
    /// - Permissions are stored only in UserPermissions.
    /// - PharmacyPermissionRoles are NOT used.
    /// - PharmacyRolePermissions are NOT used.
    /// - All permissions are TRUE by default.
    /// - Existing permissions are NEVER overwritten.
    /// - Permissions are scoped to standalone pharmacy accounts.
    /// </summary>
    public async Task CreateDefaultPermissionsAsync(
        int userId,
        string role,
        int assignedByUserId)
    {
        // ---------------------------------------------------------
        // 1. VALIDATION
        // ---------------------------------------------------------

        if (userId <= 0)
        {
            throw new ArgumentException(
                "Invalid user id.",
                nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException(
                "Role is required.",
                nameof(role));
        }

        // ---------------------------------------------------------
        // 2. VALIDATE USER
        // ---------------------------------------------------------

        var user = await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.Id == userId &&
                x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Role,
                x.PharmacyId,
                x.BranchId
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            throw new InvalidOperationException(
                "User was not found or is inactive.");
        }

        // Standalone Pharmacy users must belong to a Pharmacy.
        if (!user.PharmacyId.HasValue)
        {
            throw new InvalidOperationException(
                "User must be assigned to a pharmacy before permissions can be created.");
        }

        // Standalone permissions are not scoped to a hospital.

        // ---------------------------------------------------------
        // 4. GET MODULES FOR USER ROLE
        // ---------------------------------------------------------

        string[] modules;

        if (role.Equals(
                UserRoles.PharmacyAdmin,
                StringComparison.OrdinalIgnoreCase))
        {
            modules = PermissionModules.PharmacyAdmin;
        }
        else if (role.Equals(
                     UserRoles.Pharmacist,
                     StringComparison.OrdinalIgnoreCase))
        {
            modules = PermissionModules.Pharmacist;
        }
        else
        {
            // Permission service currently supports only:
            //
            // PharmacyAdmin
            // Pharmacist
            //
            // Super Admin does not need UserPermissions.
            return;
        }

        // ---------------------------------------------------------
        // 5. REMOVE EMPTY / DUPLICATE MODULES
        // ---------------------------------------------------------

        modules = modules
            .Where(x =>
                !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (modules.Length == 0)
            return;

        // ---------------------------------------------------------
        // 6. GET EXISTING USER PERMISSIONS
        // ---------------------------------------------------------
        //
        // Existing permissions are NOT changed.
        //
        // Example:
        //
        // Branches:
        // CanView = false
        //
        // Calling this method again WILL NOT change it
        // back to true.
        // ---------------------------------------------------------

        var existingModules =
            await _context.UserPermissions
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId)
                .Select(x => x.Module)
                .ToListAsync();

        var existingModuleSet =
            new HashSet<string>(
                existingModules
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase);

        // ---------------------------------------------------------
        // 7. CREATE ONLY MISSING PERMISSIONS
        // ---------------------------------------------------------

        var now = DateTime.UtcNow;

        var permissionsToCreate =
            modules
                .Where(module =>
                    !existingModuleSet.Contains(module))
                .Select(module =>
                    new UserPermission
                    {
                        UserId = userId,

                        // Permissions are pharmacy-user scoped.

                        Module = module,

                        // -----------------------------------------
                        // DEFAULT PERMISSIONS = TRUE
                        // -----------------------------------------

                        CanView = true,
                        CanCreate = true,
                        CanEdit = true,
                        CanDelete = true,

                        // -----------------------------------------
                        // AUDIT
                        // -----------------------------------------

                        AssignedByUserId =
                            assignedByUserId,

                        UpdatedAt = now
                    })
                .ToList();

        if (permissionsToCreate.Count == 0)
            return;

        await _context.UserPermissions
            .AddRangeAsync(
                permissionsToCreate);

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Ensures all required permission modules exist.
    ///
    /// Missing modules are created TRUE.
    /// Existing permissions remain unchanged.
    /// </summary>
    public async Task EnsureDefaultPermissionsAsync(
        int userId,
        string role,
        int assignedByUserId)
    {
        await CreateDefaultPermissionsAsync(
            userId,
            role,
            assignedByUserId);
    }

    /// <summary>
    /// Returns all permissions assigned directly to a user.
    /// </summary>
    public async Task<List<UserPermission>>
        GetUserPermissionsAsync(
            int userId)
    {
        if (userId <= 0)
        {
            return new List<UserPermission>();
        }

        return await _context.UserPermissions
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId)
            .OrderBy(x =>
                x.Module)
            .ToListAsync();
    }

    /// <summary>
    /// Checks whether a user has a specific permission.
    ///
    /// Missing permission = FALSE.
    /// </summary>
    public async Task<bool> HasPermissionAsync(
        int userId,
        string module,
        string action)
    {
        if (userId <= 0 ||
            string.IsNullOrWhiteSpace(module) ||
            string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        var normalizedModule =
            PharmacyPermissionChecker
                .ResolveAdminSidebarModule(
                    module);

        if (string.IsNullOrWhiteSpace(
                normalizedModule))
        {
            return false;
        }

        var permissions =
            await _context.UserPermissions
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId)
                .Select(x => new
                {
                    x.Module,
                    x.CanView,
                    x.CanCreate,
                    x.CanEdit,
                    x.CanDelete
                })
                .ToListAsync();

        var matchingPermissions =
            permissions
                .Where(x =>
                    string.Equals(
                        PharmacyPermissionChecker
                            .ResolveAdminSidebarModule(
                                x.Module),
                        normalizedModule,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        // Missing permission = NO ACCESS.
        if (matchingPermissions.Count == 0)
            return false;

        return action
            .Trim()
            .ToLowerInvariant() switch
        {
            "view" =>
                matchingPermissions.Any(
                    x => x.CanView),

            "create" =>
                matchingPermissions.Any(
                    x => x.CanCreate),

            "edit" =>
                matchingPermissions.Any(
                    x => x.CanEdit),

            "delete" =>
                matchingPermissions.Any(
                    x => x.CanDelete),

            _ => false
        };
    }

    /// <summary>
    /// Updates one user's module permission.
    ///
    /// Example:
    ///
    /// UserId = 2
    /// Module = Branches
    /// CanView = false
    /// CanCreate = true
    /// CanEdit = true
    /// CanDelete = true
    /// </summary>
    public async Task<bool> UpdatePermissionAsync(
        int userId,
        string module,
        bool canView,
        bool canCreate,
        bool canEdit,
        bool canDelete,
        int updatedByUserId)
    {
        if (userId <= 0 ||
            string.IsNullOrWhiteSpace(module))
        {
            return false;
        }

        var normalizedModule =
            PharmacyPermissionChecker
                .ResolveAdminSidebarModule(
                    module);

        var permissions =
            await _context.UserPermissions
                .Where(x =>
                    x.UserId == userId)
                .ToListAsync();

        var permission =
            permissions.FirstOrDefault(x =>
                string.Equals(
                    PharmacyPermissionChecker
                        .ResolveAdminSidebarModule(
                            x.Module),
                    normalizedModule,
                    StringComparison.OrdinalIgnoreCase));

        if (permission == null)
        {
            return false;
        }

        permission.CanView =
            canView;

        permission.CanCreate =
            canCreate;

        permission.CanEdit =
            canEdit;

        permission.CanDelete =
            canDelete;

        permission.AssignedByUserId =
            updatedByUserId;

        permission.UpdatedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Returns permissions together with user identity.
    ///
    /// This avoids storing duplicate UserName/Role columns
    /// inside UserPermissions.
    /// </summary>
    public async Task<object?>
        GetUserPermissionsWithUserAsync(
            int userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.Id == userId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.Role,
                x.PharmacyId,
                x.BranchId,
                x.AccountStatus,
                x.IsActive
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return null;

        var permissions =
            await _context.UserPermissions
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId)
                .OrderBy(x =>
                    x.Module)
                .Select(x => new
                {
                    x.Module,
                    x.CanView,
                    x.CanCreate,
                    x.CanEdit,
                    x.CanDelete,
                    x.AssignedByUserId,
                    x.UpdatedAt
                })
                .ToListAsync();

        return new
        {
            UserId = user.Id,

            UserName = user.Name,

            user.Email,

            user.Role,

            user.PharmacyId,

            user.BranchId,

            user.AccountStatus,

            user.IsActive,

            Permissions = permissions
        };
    }
}
