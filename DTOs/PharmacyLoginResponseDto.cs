namespace AuthDemo.DTOs;

public class PharmacyLoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? PharmacyId { get; set; }
    public string PharmacyName { get; set; } = string.Empty;
    public string OperationMode { get; set; } = "Standalone";
    public int? BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
    public string AccountStatus { get; set; } = "PendingPharmacyAssignment";
    public bool IsPharmacyAssigned { get; set; }
    public bool CanAccessPharmacyOperations { get; set; }
    public List<PharmacyPermissionDto> Permissions { get; set; } = new();
}

public class PharmacyPermissionDto
{
    public string Module { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}
