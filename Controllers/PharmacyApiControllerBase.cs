using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthDemo.Controllers;

public abstract class PharmacyApiControllerBase : ControllerBase
{
    // =====================================================
    // CURRENT USER ID
    // =====================================================

    protected int UserId
    {
        get
        {
            var value =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ??
                User.FindFirst("userId")?.Value;

            if (!int.TryParse(value, out var userId) ||
                userId <= 0)
            {
                throw new UnauthorizedAccessException(
                    "User id claim is missing or invalid.");
            }

            return userId;
        }
    }


    // =====================================================
    // CURRENT ROLE
    // =====================================================

    protected string CurrentRole
    {
        get
        {
            return User.FindFirst(ClaimTypes.Role)?.Value
                   ??
                   User.FindFirst("role")?.Value
                   ??
                   string.Empty;
        }
    }


    // =====================================================
    // ROLE HELPERS
    // =====================================================

    protected bool IsPharmacySuperAdmin =>
        User.IsInRole("PharmacySuperAdmin") ||
        string.Equals(
            CurrentRole,
            "PharmacySuperAdmin",
            StringComparison.OrdinalIgnoreCase);


    protected bool IsPharmacyAdmin =>
        User.IsInRole("PharmacyAdmin") ||
        string.Equals(
            CurrentRole,
            "PharmacyAdmin",
            StringComparison.OrdinalIgnoreCase);


    protected bool IsPharmacist =>
        User.IsInRole("Pharmacist") ||
        string.Equals(
            CurrentRole,
            "Pharmacist",
            StringComparison.OrdinalIgnoreCase);


    // =====================================================
    // PHARMACY ID
    //
    // Standalone PMS:
    //
    // PharmacyAdmin:
    //     PharmacyId = REQUIRED
    //
    // Pharmacist:
    //     PharmacyId = REQUIRED
    //
    // PharmacyId is the primary tenant-isolation key.
    // Pharmacy and branch claims define the standalone PMS scope.
    // =====================================================

    protected int? OptionalPharmacyId
    {
        get
        {
            var value =
                User.FindFirst("PharmacyId")?.Value
                ??
                User.FindFirst("pharmacyId")?.Value;

            if (int.TryParse(
                    value,
                    out var pharmacyId) &&
                pharmacyId > 0)
            {
                return pharmacyId;
            }

            return null;
        }
    }


    protected int PharmacyId =>
        OptionalPharmacyId
        ?? throw new UnauthorizedAccessException(
            "A pharmacy assignment is required.");


    // =====================================================
    // BRANCH ID
    //
    // PharmacyAdmin:
    //
    //     BranchId is NOT permanently assigned.
    //
    //     Admin manages all branches under PharmacyId.
    //
    //     For operational screens such as:
    //     Medicines
    //     Inventory
    //     Prescriptions
    //     Billing
    //     Reports
    //
    //     the Admin must select a branch.
    //
    //
    // Pharmacist:
    //
    //     BranchId is REQUIRED.
    //
    //     Pharmacist is permanently scoped to one branch.
    // =====================================================

    protected int? OptionalBranchId
    {
        get
        {
            var value =
                User.FindFirst("BranchId")?.Value
                ??
                User.FindFirst("branchId")?.Value;

            if (int.TryParse(
                    value,
                    out var branchId) &&
                branchId > 0)
            {
                return branchId;
            }

            return null;
        }
    }


    /// <summary>
    /// Returns the branch stored in the JWT.
    ///
    /// Normally this is used for Pharmacist.
    ///
    /// PharmacyAdmin should use selected branch context
    /// instead of expecting BranchId in the JWT.
    /// </summary>
    protected int BranchId =>
        OptionalBranchId
        ?? throw new UnauthorizedAccessException(
            "A branch must be selected or assigned before performing this operation.");


    // =====================================================
    // ASSIGNMENT CLAIM
    // =====================================================

    protected bool HasPharmacyAssignmentClaim
    {
        get
        {
            var value =
                User.FindFirst("IsPharmacyAssigned")?.Value
                ??
                User.FindFirst("isPharmacyAssigned")?.Value;

            return string.Equals(
                value,
                "true",
                StringComparison.OrdinalIgnoreCase);
        }
    }


    // =====================================================
    // PHARMACY ASSIGNMENT
    //
    // FINAL RULES
    //
    // PharmacyAdmin:
    //
    //     PharmacyId required
    //     Pharmacy-scoped authentication
    //     BranchId NOT required
    //
    //
    // Pharmacist:
    //
    //     PharmacyId required
    //     BranchId required
    //     Pharmacy-scoped authentication
    //
    //
    // Actual active Pharmacy/Branch ownership should also
    // be validated against the database by the controller
    // before sensitive operations.
    // =====================================================

    protected bool IsPharmacyAssigned
    {
        get
        {
            if (!HasPharmacyAssignmentClaim)
            {
                return false;
            }

            if (!OptionalPharmacyId.HasValue)
            {
                return false;
            }

            // ---------------------------------------------
            // PHARMACY ADMIN
            //
            // Admin belongs to entire PharmacyTenant.
            // No branch assignment required.
            // ---------------------------------------------

            if (IsPharmacyAdmin)
            {
                return true;
            }


            // ---------------------------------------------
            // PHARMACIST
            //
            // Pharmacist belongs to Pharmacy + Branch.
            // Authorization uses pharmacy and branch scope.
            // ---------------------------------------------

            if (IsPharmacist)
            {
                return OptionalBranchId.HasValue;
            }


            // ---------------------------------------------
            // SUPER ADMIN
            //
            // Super Admin does not need PharmacyId-based
            // assignment to access its own controller.
            // ---------------------------------------------

            return false;
        }
    }


    // =====================================================
    // BRANCH ASSIGNMENT CHECK
    //
    // Mainly useful for Pharmacist.
    //
    // PharmacyAdmin normally has no BranchId claim.
    // =====================================================

    protected bool HasBranchAssignment =>
        OptionalBranchId.HasValue;


    // =====================================================
    // REQUIRE ASSIGNED BRANCH
    //
    // Use for Pharmacist or any operation where BranchId
    // must already exist in the authenticated token.
    //
    // Do NOT use this to force PharmacyAdmin to have a
    // permanent branch.
    // =====================================================

    protected int RequireBranchId()
    {
        if (!OptionalBranchId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "A branch must be selected or assigned before performing this operation.");
        }

        return OptionalBranchId.Value;
    }


    // =====================================================
    // RESOLVE OPERATIONAL BRANCH ID
    //
    // PharmacyAdmin:
    //     Uses selectedBranchId supplied by the request.
    //
    // Pharmacist:
    //     Uses BranchId assigned in JWT.
    //
    // IMPORTANT:
    // The controller must still verify in the database that
    // the returned BranchId belongs to PharmacyId.
    // =====================================================

    protected int ResolveOperationalBranchId(
        int? selectedBranchId = null)
    {
        // Pharmacy Admin manages multiple branches.
        // Operational APIs therefore require a selected branch.
        if (IsPharmacyAdmin)
        {
            if (!selectedBranchId.HasValue ||
                selectedBranchId.Value <= 0)
            {
                throw new UnauthorizedAccessException(
                    "Please select a branch before performing this operation.");
            }

            return selectedBranchId.Value;
        }


        // Pharmacist is already assigned to a branch.
        if (IsPharmacist)
        {
            return RequireBranchId();
        }


        throw new UnauthorizedAccessException(
            "A valid pharmacy role is required for this operation.");
    }


    // =====================================================
    // CHECK WHETHER BRANCH MATCHES CURRENT CONTEXT
    //
    // This is only a basic claim/request comparison helper.
    //
    // Database validation:
    //
    // Branch.Id == branchId
    // AND
    // Branch.PharmacyId == PharmacyId
    //
    // must still be performed by the calling controller.
    // =====================================================

    protected bool IsBranchAllowedByCurrentContext(
        int branchId)
    {
        if (branchId <= 0)
        {
            return false;
        }

        // PharmacyAdmin may select any branch belonging
        // to its PharmacyId. Database validation decides
        // whether that ownership is valid.
        if (IsPharmacyAdmin)
        {
            return OptionalPharmacyId.HasValue;
        }

        // Pharmacist cannot switch to another branch.
        if (IsPharmacist)
        {
            return OptionalPharmacyId.HasValue &&
                   OptionalBranchId.HasValue &&
                   OptionalBranchId.Value == branchId;
        }

        return false;
    }
}