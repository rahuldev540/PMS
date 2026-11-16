using System.Diagnostics;

using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class UnifiedAuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtHelper _jwtHelper;
    private readonly ILogger<UnifiedAuthController> _logger;

    public UnifiedAuthController(
        AppDbContext db,
        JwtHelper jwtHelper,
        ILogger<UnifiedAuthController> logger)
    {
        _db = db;
        _jwtHelper = jwtHelper;
        _logger = logger;
    }

    // ============================================================
    // UNIFIED LOGIN
    // POST /api/auth/login
    //
    // Single login for:
    // - PharmacySuperAdmin
    // - PharmacyAdmin
    // - Pharmacist
    // ============================================================

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginDto dto)
    {
        var loginWatch = Stopwatch.StartNew();
        var login =
            dto.Email?.Trim();

        if (string.IsNullOrWhiteSpace(login) ||
            string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Email/mobile number and password are required."
            });
        }

        var normalized =
            login.ToLowerInvariant();

        var user =
            await _db.Users
                .AsNoTracking()
                .Include(x => x.Branch)
                .Include(x => x.Pharmacy)
                .FirstOrDefaultAsync(x =>
                    x.IsActive &&
                    (
                        x.Role == "PharmacySuperAdmin" ||
                        x.Role == "PharmacyAdmin" ||
                        x.Role == "Pharmacist"
                    ) &&
                    (
                        (
                            x.Email != null &&
                            x.Email.ToLower() == normalized
                        ) ||
                        x.MobileNumber == login
                    ));

        if (user == null ||
            string.IsNullOrWhiteSpace(user.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash))
        {
            return Unauthorized(new
            {
                Success = false,
                Message = "Invalid credentials."
            });
        }

        // --------------------------------------------------------
        // ASSIGNMENT RULES
        //
        // PharmacySuperAdmin:
        // - No pharmacy/branch assignment required.
        //
        // PharmacyAdmin:
        // - Active account
        // - PharmacyId required
        // - Active PharmacyTenant required
        // - No hospital assignment required
        // - BranchId NOT required (Admin manages all branches
        //   belonging to the assigned pharmacy).
        //
        // Pharmacist:
        // - Active account
        // - PharmacyId required
        // - BranchId required
        // - Branch must belong to PharmacyId and be active
        // - No hospital assignment required
        // --------------------------------------------------------

        var assigned =
            user.Role switch
            {
                "PharmacySuperAdmin" => true,

                "PharmacyAdmin" =>
                    (user.AccountStatus == "Pending" && !user.PharmacyId.HasValue ||
                     user.AccountStatus == "Active" && user.PharmacyId.HasValue),

                "Pharmacist" =>
                    user.AccountStatus == "Active" &&
                    user.PharmacyId.HasValue &&
                    user.BranchId.HasValue,

                _ => false
            };

        // Validate the Pharmacy Admin's PharmacyTenant.
        // Standalone Admin does not require a branch assignment.
        if (user.Role == "PharmacyAdmin" && assigned && user.PharmacyId.HasValue)
        {
            var validPharmacy =
                await _db.Pharmacies
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.Id == user.PharmacyId!.Value &&
                        x.IsActive);

            if (!validPharmacy)
            {
                assigned = false;
            }
        }

        // Pharmacists remain branch scoped.
        // The branch must belong to the same PharmacyTenant.
        if (user.Role == "Pharmacist" && assigned)
        {
            var validPharmacy =
                await _db.Pharmacies
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.Id == user.PharmacyId!.Value &&
                        x.IsActive);

            var validBranch =
                validPharmacy &&
                await _db.Branches
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.Id == user.BranchId!.Value &&
                        x.PharmacyId == user.PharmacyId!.Value &&
                        x.IsActive);

            if (!validBranch)
            {
                assigned = false;
            }
        }

        if (!assigned)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                Success = false,
                Message = "Account is inactive, or its pharmacy/branch assignment is invalid."
            });
        }

        // Public API branch identifiers are scoped to the pharmacy. Keep the
        // stored User.BranchId and JWT claims internal for authorization.
        int? publicBranchId = null;
        if (user.PharmacyId.HasValue && user.BranchId.HasValue)
        {
            publicBranchId = await PharmacyPublicBranchMapper.GetPublicBranchIdAsync(
                _db, user.PharmacyId.Value, user.BranchId.Value);
        }

        // Load actual DB permissions before returning login response.
        var permissions =
            await LoadPermissions(user);

        // The sidebar is an effective-permission view, not the full role menu.
        // Keep the existing response shape and module ordering for the frontend.
        // Missing permissions are denied rather than silently shown as enabled.
        var allowedModules = new HashSet<string>(
            permissions.Where(p => p.CanView).Select(p => p.Module),
            StringComparer.OrdinalIgnoreCase);
        var visibleSidebarModules =
            (user.Role == "PharmacyAdmin" ? AdminModules : PharmacistModules)
            .Where(module => allowedModules.Contains(module))
            .ToArray();

        var token =
            _jwtHelper.GenerateToken(user);

        // --------------------------------------------------------
        // Login audit
        // --------------------------------------------------------

        var audit = new AuditLog
            {
                UserId =
                    user.Id,

                UserName =
                    user.Name ??
                    string.Empty,

                Role =
                    user.Role ??
                    string.Empty,


                BranchId =
                    user.BranchId,

                Action =
                    "Login",

                SystemAction =
                    $"{user.Role} Login",

                IsLoginActivity =
                    true,

                IpAddress =
                    GetClientIpAddress(),

                Browser =
                    Request.Headers.UserAgent
                        .ToString(),

                Device =
                    "Unknown",

                LoginTime =
                    DateTime.UtcNow,

                IsOnline =
                    true,

                Timestamp =
                    DateTime.UtcNow
            };

        // Audit persistence is best-effort: a transient audit-table failure must
        // not convert valid credentials into a failed login response.
        // Keep the audit write bounded independently of the global SQL timeout.
        var previousTimeout = _db.Database.GetCommandTimeout();
        try
        {
            _db.Database.SetCommandTimeout(5);
            _db.AuditLogs.Add(audit);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException || ex is TimeoutException ||
                                   ex is Microsoft.Data.SqlClient.SqlException ||
                                   ex is InvalidOperationException)
        {
            _logger.LogError(ex, "Login audit persistence failed; correlation {TraceId}", HttpContext.TraceIdentifier);
            _db.Entry(audit).State = EntityState.Detached;
        }
        finally
        {
            _db.Database.SetCommandTimeout(previousTimeout);
        }

        _logger.LogInformation("Login accepted for role {Role}; trace {TraceId}; duration {ElapsedMs} ms",
            user.Role, HttpContext.TraceIdentifier, loginWatch.ElapsedMilliseconds);

        // Global Super Admin has no pharmacy/branch tenant context.
        // Return a role-specific shape so tenant properties are absent,
        // rather than present with null/empty values.
        if (user.Role == "PharmacySuperAdmin")
        {
            return Ok(new
            {
                Success = true,
                Message = "Login successful.",
                Token = token,
                User = new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    user.MobileNumber,
                    user.Role,
                    RoleDisplayName = "Super Admin",
                    Console = "SuperAdmin",
                    SidebarModules = SuperAdminModules,
                    OperationMode = "Standalone",
                    user.MustChangePassword,
                    user.AccountStatus,
                    Permissions = permissions
                }
            });
        }

        return Ok(new
        {
            Success = true,

            Message =
                "Login successful.",

            Token =
                token,

            User = new
            {
                user.Id,
                user.Name,
                user.Email,
                user.MobileNumber,
                user.Role,

                RoleDisplayName =
                    user.Role ==
                        "PharmacySuperAdmin"
                        ? "Super Admin"
                        : user.Role ==
                            "PharmacyAdmin"
                            ? "Admin"
                            : "Pharmacist",

                Console =
                    user.Role ==
                        "PharmacySuperAdmin"
                        ? "SuperAdmin"
                        : user.Role ==
                            "PharmacyAdmin"
                            ? "Admin"
                            : "Pharmacist",

                // Only modules granted CanView are advertised to the frontend.
                // Backend API authorization remains the security boundary.
                SidebarModules = visibleSidebarModules,

                user.PharmacyId,

                PharmacyName = user.Pharmacy?.Name ?? string.Empty,

                OperationMode = "Standalone",

                // Admin normally has no permanent BranchId.
                // Pharmacist is permanently branch scoped.
                BranchId = publicBranchId,

                BranchName =
                    user.Branch?
                        .Name ??
                    string.Empty,

                user.MustChangePassword,
                user.AccountStatus,

                IsPharmacyAssigned =
                    user.PharmacyId.HasValue && assigned,

                RequiresPharmacyRegistration =
                    user.Role == "PharmacyAdmin" && !user.PharmacyId.HasValue,

                CanAccessPharmacyOperations =
                    assigned && (user.Role != "PharmacyAdmin" || user.PharmacyId.HasValue),

                RequiresBranchSelection =
                    assigned &&
                    user.Role == "PharmacyAdmin",

                Permissions =
                    permissions
            }
        });
    }

    // ============================================================
    // LOAD PERMISSIONS
    // ============================================================

    private async Task<List<PharmacyPermissionDto>>
        LoadPermissions(User user)
    {
        // --------------------------------------------------------
        // SUPER ADMIN
        //
        // Super Admin has full access to all Super Admin modules.
        // --------------------------------------------------------

        if (user.Role == "PharmacySuperAdmin")
        {
            return SuperAdminModules
                .Select(FullPermission)
                .ToList();
        }

        // --------------------------------------------------------
        // PHARMACY ADMIN
        //
        // Permissions come from the role assigned by Super Admin.
        //
        // IMPORTANT:
        // Older/internal module names are normalized here:
        //
        // Inventory        -> Stock
        // Purchases        -> Purchase Orders
        // User Management  -> Users
        // Users & Permissions -> Users
        // --------------------------------------------------------

        if (user.Role == "PharmacyAdmin")
        {
            var savedPermissions = await _db.UserPermissions
                .AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .Select(x => new
                {
                    x.Module, x.CanView, x.CanCreate, x.CanEdit, x.CanDelete
                })
                .ToListAsync();

            return savedPermissions
                .Select(x => new PharmacyPermissionDto
                {
                    Module = NormalizeAdminModule(x.Module),
                    CanView = x.CanView,
                    CanCreate = x.CanCreate,
                    CanEdit = x.CanEdit,
                    CanDelete = x.CanDelete
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Module))
                .GroupBy(x => x.Module, StringComparer.OrdinalIgnoreCase)
                .Select(g => new PharmacyPermissionDto
                {
                    Module = g.Key,
                    CanView = g.Any(x => x.CanView),
                    CanCreate = g.Any(x => x.CanCreate),
                    CanEdit = g.Any(x => x.CanEdit),
                    CanDelete = g.Any(x => x.CanDelete)
                })
                .OrderBy(x => x.Module)
                .ToList();
        }

        // --------------------------------------------------------
        // PHARMACIST
        //
        // Pharmacist permissions are individual UserPermissions.
        // --------------------------------------------------------

        // Standalone pharmacist permissions are per-user.
        // Pharmacy and branch claims determine standalone authorization scope.
        var query =
            _db.UserPermissions
                .AsNoTracking()
                .Where(x =>
                    x.UserId ==
                    user.Id);

        var pharmacistSaved =
            await query
                .Select(x => new
                {
                    x.Module,
                    x.CanView,
                    x.CanCreate,
                    x.CanEdit,
                    x.CanDelete
                })
                .ToListAsync();

        return pharmacistSaved
            .Where(x =>
                PharmacistModules.Contains(
                    x.Module,
                    StringComparer
                        .OrdinalIgnoreCase))
            .Select(x =>
                new PharmacyPermissionDto
                {
                    Module =
                        x.Module,

                    CanView =
                        x.CanView,

                    CanCreate =
                        x.CanCreate,

                    CanEdit =
                        x.CanEdit,

                    CanDelete =
                        x.CanDelete
                })
            .OrderBy(x =>
                Array.IndexOf(
                    PharmacistModules,
                    x.Module))
            .ToList();
    }

    // ============================================================
    // ADMIN MODULE NORMALIZATION
    // ============================================================

    private static string NormalizeAdminModule(
        string? module)
    {
        if (string.IsNullOrWhiteSpace(module))
            return string.Empty;

        return module
            .Trim()
            .ToLowerInvariant()
            switch
        {
            "dashboard" =>
                "Dashboard",

            "branch" =>
                "Branches",

            "branches" =>
                "Branches",

            "medicine" =>
                "Medicines",

            "medicines" =>
                "Medicines",

            // Inventory APIs belong to Stock.
            "inventory" =>
                "Stock",

            "stock" =>
                "Stock",

            // Keep frontend module supported.
            "expiry alerts" =>
                "Expiry Alerts",

            "expiry" =>
                "Expiry Alerts",

            "supplier" =>
                "Suppliers",

            "suppliers" =>
                "Suppliers",

            // Purchase APIs belong to Purchase Orders.
            "purchase" =>
                "Purchase Orders",

            "purchases" =>
                "Purchase Orders",

            "purchase orders" =>
                "Purchase Orders",

            "stock transfer" =>
                "Stock Transfers",

            "stock transfers" =>
                "Stock Transfers",

            "prescription" =>
                "Prescriptions",

            "prescriptions" =>
                "Prescriptions",

            "pending" =>
                "Prescriptions",

            "sales" =>
                "Dispensing",

            "dispensing" =>
                "Dispensing",

            "billing" =>
                "Dispensing",

            "bills" =>
                "Dispensing",

            "returns" =>
                "Dispensing",

            // User APIs belong to Users.
            "user" =>
                "Users",

            "users" =>
                "Users",

            "user management" =>
                "Users",

            "users & permissions" =>
                "Users",

            "report" =>
                "Reports",

            "reports" =>
                "Reports",

            "settings" =>
                "Settings",

            "system settings" =>
                "Settings",

            _ =>
                module.Trim()
        };
    }

    // ============================================================
    // FULL PERMISSION
    // Used only for Pharmacy Super Admin.
    // ============================================================

    private static PharmacyPermissionDto
        FullPermission(string module)
    {
        return new PharmacyPermissionDto
        {
            Module =
                module,

            CanView =
                true,

            CanCreate =
                true,

            CanEdit =
                true,

            CanDelete =
                true
        };
    }

    // ============================================================
    // CLIENT IP
    // ============================================================

    private string GetClientIpAddress()
    {
        var forwardedFor =
            Request.Headers[
                    "X-Forwarded-For"]
                .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(
                forwardedFor))
        {
            var firstIp =
                forwardedFor
                    .Split(',')
                    .FirstOrDefault()?
                    .Trim();

            if (!string.IsNullOrWhiteSpace(
                    firstIp))
            {
                return firstIp;
            }
        }

        var realIp =
            Request.Headers[
                    "X-Real-IP"]
                .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(
                realIp))
        {
            return realIp.Trim();
        }

        var remoteIp =
            HttpContext.Connection
                .RemoteIpAddress;

        if (remoteIp == null)
            return string.Empty;

        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp =
                remoteIp.MapToIPv4();
        }

        var ip =
            remoteIp.ToString();

        if (ip == "::1")
            return "127.0.0.1";

        return ip;
    }

    // ============================================================
    // SIDEBAR MODULE DEFINITIONS
    // ============================================================

    internal static readonly string[]
        SuperAdminModules =
    {
        "Dashboard",
        "Clinics",
        "Branches",
        "Medicines",
        "Admins",
        "Users & Permissions",
        "System Settings",
        "Reports",
        "Audit Logs",
        "Notifications"
    };

    // Pharmacy Admin modules.
    //
    // These names should also be used by the Super Admin
    // role-permission screen.
    internal static readonly string[]
        AdminModules = AuthDemo.Constants.PermissionModules.PharmacyAdmin;

    internal static readonly string[]
        PharmacistModules = AuthDemo.Constants.PermissionModules.Pharmacist;
}