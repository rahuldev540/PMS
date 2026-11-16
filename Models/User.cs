
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

public class User
{
    // =====================================================
    // PRIMARY KEY
    // =====================================================

    public int Id { get; set; }

    // =====================================================
    // PHARMACY TENANT
    // =====================================================

    // Standalone PharmacySuperAdmin:
    // PharmacyId = NULL
    // BranchId = NULL
    // HospitalId = NULL

    // PharmacyAdmin:
    // PharmacyId and BranchId are assigned later.

    // Pharmacist:
    // PharmacyId and BranchId are required.

    public int? PharmacyId { get; set; }

    public PharmacyTenant? Pharmacy { get; set; }

    // =====================================================
    // PERMISSIONS
    // =====================================================

    // User-specific permissions are stored in
    // UserPermissions using UserPermission.UserId.
    //
    // PharmacyPermissionRoleId and its navigation
    // property have been removed.

    // =====================================================
    // BASIC DETAILS
    // =====================================================

    [MaxLength(150)]
    public string? Name { get; set; }

    [MaxLength(20)]
    public string? MobileNumber { get; set; }

    [MaxLength(256)]
    public string? Email { get; set; }

    // =====================================================
    // ADDRESS DETAILS - NEW
    // =====================================================

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    // =====================================================
    // AUTHENTICATION
    // =====================================================

    public string? PasswordHash { get; set; }

    [MaxLength(64)]
    public string? Role { get; set; }

    // =====================================================
    // DOCTOR LINK
    // =====================================================

    // Retained for existing database compatibility.

    public int? DoctorId { get; set; }

    // =====================================================
    // LEGACY NULLABLE ASSIGNMENT FIELD
    // =====================================================


    // =====================================================
    // BRANCH ASSIGNMENT
    // =====================================================

    // PharmacyAdmin receives Main Branch after assignment.
    // Pharmacist receives an assigned working branch.

    public int? BranchId { get; set; }

    public Branch? Branch { get; set; }

    // =====================================================
    // STATUS
    // =====================================================

    public bool IsActive { get; set; } = true;

    [MaxLength(50)]
    public string AccountStatus { get; set; } = "Active";

    // =====================================================
    // PHARMACY ASSIGNMENT AUDIT
    // =====================================================

    public DateTime? PharmacyAssignedAt { get; set; }

    public int? PharmacyAssignedByUserId { get; set; }

    // =====================================================
    // CREATED BY
    // =====================================================

    public int? CreatedByUserId { get; set; }

    // =====================================================
    // CREATED DATE
    // =====================================================

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    // =====================================================
    // PASSWORD MANAGEMENT
    // =====================================================

    public bool MustChangePassword { get; set; } = true;

    // =====================================================
    // PASSWORD RESET OTP
    // =====================================================

    public string? PasswordResetOtpHash { get; set; }

    public DateTime? PasswordResetOtpExpiresAt { get; set; }

    public DateTime? PasswordResetOtpLastSentAt { get; set; }

    public int PasswordResetOtpAttempts { get; set; }

    // =====================================================
    // PASSWORD RESET TOKEN
    // =====================================================

    public string? PasswordResetTokenHash { get; set; }

    public DateTime? PasswordResetTokenExpiresAt { get; set; }
}
