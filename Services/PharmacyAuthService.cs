using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;
using AuthDemo.Services.Interfaces;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

public class PharmacyAuthService : IPharmacyAuthService
{
    private readonly AppDbContext _context;
    private readonly JwtHelper _jwtHelper;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PharmacyAuthService(
        AppDbContext context,
        JwtHelper jwtHelper,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _jwtHelper = jwtHelper;
        _httpContextAccessor = httpContextAccessor;
    }

    // =====================================================
    // PHARMACIST LOGIN
    // =====================================================

    public async Task<PharmacyLoginResponseDto?> LoginAsync(
        LoginDto dto)
    {
        var login =
            dto.Email?.Trim();

        if (string.IsNullOrWhiteSpace(login) ||
            string.IsNullOrWhiteSpace(dto.Password))
        {
            return null;
        }

        var normalizedLogin =
            login.ToLowerInvariant();

        // =================================================
        // FIND ACTIVE PHARMACIST
        // =================================================

        var user =
            await _context.Users
                .Include(x => x.Branch)
                .Include(x => x.Pharmacy)
                .FirstOrDefaultAsync(x =>
                    x.IsActive &&
                    x.Role == "Pharmacist" &&
                    (
                        (
                            x.Email != null &&
                            x.Email.ToLower() ==
                            normalizedLogin
                        )
                        ||
                        x.MobileNumber == login
                    ));

        if (user == null ||
            string.IsNullOrWhiteSpace(
                user.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash))
        {
            return null;
        }

        // =================================================
        // CHECK PHARMACY ASSIGNMENT
        // =================================================

        var isAssigned =
            user.AccountStatus == "Active" &&
            user.PharmacyId.HasValue &&
            user.BranchId.HasValue;

        // =================================================
        // VALIDATE ASSIGNED BRANCH
        //
        // Pharmacy + Branch IDs being present is not enough.
        // The branch must still exist, belong to the same
        // pharmacy and remain active.
        // =================================================

        if (isAssigned)
        {
            var branchIsValid =
                await _context.Branches
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.Id ==
                        user.BranchId!.Value &&
                        x.PharmacyId ==
                        user.PharmacyId!.Value &&
                        x.IsActive);

            if (!branchIsValid)
            {
                // Do not issue an operational JWT for an
                // invalid/inactive pharmacy assignment.
                return null;
            }
        }

        // =================================================
        // LOAD PHARMACIST PERMISSIONS
        //
        // Pharmacist permissions are user-specific.
        // They are separate from Pharmacy Admin
        // PharmacyPermissionRole permissions.
        // =================================================

        var permissionsQuery =
            _context.UserPermissions
                .AsNoTracking()
                .Where(x =>
                    x.UserId ==
                    user.Id);

        // Standalone permissions are user-specific and pharmacy-scoped.

        var permissions =
            await permissionsQuery
                .OrderBy(x =>
                    x.Module)
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
                .ToListAsync();

        // =================================================
        // AUDIT LOGIN
        // =================================================

        var http =
            _httpContextAccessor.HttpContext;

        var now =
            DateTime.UtcNow;

        _context.AuditLogs.Add(
            new AuditLog
            {
                UserId =
                    user.Id,

                UserName =
                    user.Name ??
                    string.Empty,

                Role =
                    "Pharmacist",


                BranchId =
                    user.BranchId,

                Action =
                    "Login",

                SystemAction =
                    "Pharmacist Login",

                IsLoginActivity =
                    true,

                IpAddress =
                    http?.Connection
                        .RemoteIpAddress?
                        .ToString(),

                Browser =
                    http?.Request
                        .Headers
                        .UserAgent
                        .ToString(),

                Device =
                    "Unknown",

                LoginTime =
                    now,

                IsOnline =
                    true,

                Timestamp =
                    now
            });

        await _context.SaveChangesAsync();

        // =================================================
        // GENERATE JWT
        // =================================================

        var token =
            _jwtHelper.GenerateToken(
                user);

        // =================================================
        // LOGIN RESPONSE
        // =================================================

        return new PharmacyLoginResponseDto
        {
            Token =
                token,

            Name =
                user.Name ??
                string.Empty,

            Email =
                user.Email ??
                string.Empty,

            Role =
                "Pharmacist",

            PharmacyId =
                user.PharmacyId,

            PharmacyName =
                user.Pharmacy?.Name ?? string.Empty,

            OperationMode =
                "Standalone",

            BranchId =
                user.BranchId,

            BranchName =
                user.Branch?.Name ??
                string.Empty,

            MustChangePassword =
                user.MustChangePassword,

            AccountStatus =
                user.AccountStatus,

            IsPharmacyAssigned =
                isAssigned,

            CanAccessPharmacyOperations =
                isAssigned,

            Permissions =
                permissions
        };
    }
}