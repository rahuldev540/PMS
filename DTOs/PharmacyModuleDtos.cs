using DocumentFormat.OpenXml.Wordprocessing;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;


// ============================================================
// PHARMACY MASTER CRUD
// ============================================================
public record CreatePharmacyDto(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    [Phone] string? Phone,
    [EmailAddress] string? Email,
    string? Address, string? City, string? State, string? District,
    string? Country, string? PostalCode
);

public record UpdatePharmacyDto(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    [Phone] string? Phone,
    [EmailAddress] string? Email,
    string? Address, string? City, string? State, string? District,
    string? Country, string? PostalCode, bool IsActive
);

// ============================================================
// PHARMACY ADMIN
// ============================================================


public record CreatePharmacyAdminDto(
    [Required] string Name,

    [Required, EmailAddress]
    string Email,

    string? MobileNumber,

    string? Address,

    string? City,

    string? State,

    string? Country,

    string? PostalCode
);


public record UpdatePharmacyAdminDto(
    string? Name,
    string? PharmacyName,

    [EmailAddress]
    string? Email,

    string? MobileNumber,

    string? PharmacyAddress,
    string? PharmacyContactNumber,

    [EmailAddress]
    string? PharmacyEmail,

    string? City,
    string? State,
    string? Country,
    string? PostalCode,

    bool? IsActive
);


// ============================================================
// PHARMACIST
// ============================================================

public record CreatePharmacistDto(
    [Required] string Name,
    [Required, EmailAddress] string Email,
    string? MobileNumber,

    // Standalone Pharmacist is branch-scoped.
    // Backend must validate that this BranchId belongs to the
    // authenticated Pharmacy Admin's PharmacyId.
    [Range(1, int.MaxValue)]
    int BranchId,

    List<PharmacyPermissionDto>? Permissions
);

public record UpdatePharmacistDto(
    [Required] string Name,
    [Required, EmailAddress] string Email,
    string? MobileNumber,

    // Optional branch reassignment. Backend must validate that
    // the branch belongs to the same PharmacyId.
    int? BranchId = null
);


// ============================================================
// PHARMACY / BRANCH ASSIGNMENT
// ============================================================

/// <summary>
/// Assigns an existing standalone PharmacyTenant to a Pharmacy Admin.
/// BranchId is intentionally not part of this DTO.
/// </summary>
public record AssignPharmacyToAdminDto(
    [Range(1, int.MaxValue)]
    int PharmacyId
);



// ============================================================
// PHARMACY BRANCH CRUD DTOs
//
// Shared branch DTOs. Pharmacy Admin branch APIs must scope every operation
// to the pharmacy assigned to the authenticated Pharmacy Admin.
// Super Admin can use branch data for global pharmacy/branch visibility.
//
// Operations:
// Create
// Read
// Update
// Delete
// Activate / Deactivate
// ============================================================

// ------------------------------------------------------------
// CREATE BRANCH
// POST /api/pharmacy/branches (Admin) or Super Admin branch endpoint
// ------------------------------------------------------------

public record CreatePharmacyBranchDto(
    // PharmacyId is derived from the authenticated Pharmacy Admin.

    [Required]
    [StringLength(200, MinimumLength = 2)]
    string Name,

    [Phone]
    [StringLength(50)]
    string? Phone,

    [EmailAddress]
    [StringLength(200)]
    string? Email,

    [StringLength(500)]
    string? Address,

    [StringLength(100)]
    string? City,

    [StringLength(100)]
    string? State,

    [StringLength(100)]
    string? District,

    [StringLength(100)]
    string? Country,

    [StringLength(20)]
    string? PostalCode
);


// ------------------------------------------------------------
// UPDATE BRANCH
// PUT /api/pharmacy-super-admin/branches/{id}
// ------------------------------------------------------------

public record UpdatePharmacyBranchDto(
    [Required]
    [StringLength(200, MinimumLength = 2)]
    string Name,

    [Phone]
    [StringLength(50)]
    string? Phone,

    [EmailAddress]
    [StringLength(200)]
    string? Email,

    [StringLength(500)]
    string? Address,

    [StringLength(100)]
    string? City,

    [StringLength(100)]
    string? State,

    [StringLength(100)]
    string? District,

    [StringLength(100)]
    string? Country,

    [StringLength(20)]
    string? PostalCode
);


// ------------------------------------------------------------
// BRANCH RESPONSE
//
// Used by:
// GET /api/pharmacy-super-admin/branches
// GET /api/pharmacy-super-admin/branches/{id}
// POST /api/pharmacy-super-admin/branches
// PUT /api/pharmacy-super-admin/branches/{id}
// ------------------------------------------------------------

public record PharmacyBranchResponseDto(
    int Id,

    int? PharmacyId,

    string Name,

    string? Phone,

    string? Email,

    string? Address,

    string? City,

    string? State,

    string? District,

    string? Country,

    string? PostalCode,

    bool IsActive,

    string? SourceSystem,

    string? ExternalBranchId,

    DateTime? CreatedAt
);


// ------------------------------------------------------------
// BRANCH STATUS
//
// PATCH /api/pharmacy-super-admin/branches/{id}/status
// ------------------------------------------------------------

public record UpdatePharmacyBranchStatusDto(
    bool IsActive
);


// ------------------------------------------------------------
// BRANCH DELETE RESPONSE
//
// DELETE /api/pharmacy-super-admin/branches/{id}
// ------------------------------------------------------------

public record DeletePharmacyBranchResponseDto(
    bool Success,
    string Message,
    bool CanDeactivate
);


// ============================================================
// PASSWORD
// ============================================================

public record ChangePasswordDto(
    [Required] string CurrentPassword,
    [Required] string NewPassword
);

public record ResetPasswordDto(
    [Required] string NewPassword
);


// ============================================================
// COMMON STATUS
// ============================================================

public record StatusDto(
    bool IsActive
);


// ============================================================
// SUPPLIERS
// ============================================================


public sealed class SupplierUpsertDto
{
    public string Code { get; set; } = string.Empty;

    [Required]
    public string SupplierName { get; set; } = string.Empty;

    public string ContactPerson { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress]
    public string EmailAddress { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string TaxNumber { get; set; } = string.Empty;

    public string GstTaxId
    {
        get => TaxNumber;
        set => TaxNumber = value ?? string.Empty;
    }

    public string City { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string Status { get; set; } = "Active";
}


// ============================================================
// PURCHASE ORDERS
// ============================================================

public sealed class PurchaseOrderItemRequest
{
    public int MedicineId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }
}


public sealed class CreatePurchaseOrderDto
{
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierContactPhone { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public int OrderQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public string PaymentStatus { get; set; } = "Unpaid";
    public string OrderStatus { get; set; } = "Pending";
}


public record PurchaseOrderStatusDto(
    [Required] string Status
);


// ============================================================
// PURCHASE ORDER RECEIVE
// ============================================================

public record ReceivePurchaseOrderItemDto(
    int PurchaseOrderItemId,
    string BatchNumber,
    DateTime ManufactureDate,
    DateTime ExpiryDate,
    int Quantity,
    decimal PurchasePrice,
    decimal SellingPrice,
    decimal GST,
    int MinimumStock,
    int MaximumStock,
    int ReorderLevel,
    string StorageLocation
);


public record ReceivePurchaseOrderDto(
    string SupplierInvoiceNumber,
    List<ReceivePurchaseOrderItemDto> Items
);


// ============================================================
// INVENTORY
// ============================================================

public record InventoryLevelsDto(
    int MinimumStock,
    int MaximumStock,
    int ReorderLevel,
    string StorageLocation
);


public record StockAdjustmentDto(
    int MedicineId,
    int? BatchId,

    [Required]
    string Type,

    int Quantity,

    [Required]
    string Reason
);


// ============================================================
// RETURNS
// ============================================================

public record CreateReturnDto(
    [Required] string ReturnType,
    int MedicineId,
    int? BatchId,
    int? BillId,
    int? SupplierId,
    int Quantity,
    decimal RefundAmount,
    string Reason,

    [Required]
    string Disposition
);


public record ReturnStatusDto(
    [Required] string Status
);


// ============================================================
// BATCH DISPOSAL
// ============================================================

public record DisposeBatchDto(
    int Quantity,

    [Required]
    string Reason
);


// ============================================================
// STOCK TRANSFER
// ============================================================

public sealed class CreateStockTransferDto
{
    // Pharmacy-scoped public branch numbers, as returned by the branch APIs.
    // Optional for compatibility with legacy FromLocation/ToLocation clients.
    public int? FromBranchId { get; set; }
    public int? ToBranchId { get; set; }
    public string FromLocation { get; set; } = string.Empty;
    public string ToLocation { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public int TransferQuantity { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
}


public record TransferStatusDto(
    [Required] string Status
);


// ============================================================
// REFUND
// ============================================================

public record RefundPaymentDto(
    decimal Amount,
    string Reason
);


// ============================================================
// PRESCRIPTION
// ============================================================

public record PrescriptionMedicineRequest(
    int MedicineId,
    string Dosage,
    string Instructions,
    bool Morning,
    bool Afternoon,
    bool Night,
    int Days,
    int Quantity,
    string? Notes
);


public record CreateDoctorPrescriptionDto(
    int AppointmentId,
    int PatientId,
    string Diagnosis,
    string Advice,
    string Notes,
    string? Instructions,
    DateTime? FollowUpDate,
    List<PrescriptionMedicineRequest> Medicines
);


// ============================================================
// SUPER ADMIN - SYSTEM SETTINGS
//
// Frontend:
// General Settings
// App Name
// Timezone
// Currency
// Status
// Configuration Notes
// ============================================================

public record SuperAdminSettingsDto(
    [Required] string AppName,
    [Required] string Timezone,
    [Required] string Currency,
    bool IsEnabled,
    string? ConfigurationNotes
);


// ============================================================
// SUPER ADMIN - NOTIFICATIONS
//
// Frontend:
// Send Notification
// Recipient Type
// Hospital
// Branch
// User
// ============================================================

public record SendPharmacyNotificationDto(
    [Required] string Title,
    [Required] string Message,
    string RecipientType = "All",
    int? BranchId = null,
    int? UserId = null
);


// ============================================================
// SUPER ADMIN - USER ROLES
// ============================================================

public record CreatePharmacyRoleDto(
    [Required] string Name,
    string? Description
);


public record UpdatePharmacyRoleDto(
    [Required] string Name,
    string? Description
);


// ============================================================
// SUPER ADMIN - ROLE PERMISSION MATRIX
//
// Modules:
// Dashboard
// Branches
// Medicines
// Inventory
// Purchases
// Sales
// Prescriptions
// Suppliers
// Stock Transfers
// Users & Permissions
// User Management
// Settings
// Reports
//
// Permissions:
// View / Create / Edit / Delete
// ============================================================

public record PharmacyRolePermissionDto(
    [Required] string Module,
    bool CanView,
    bool CanCreate,
    bool CanEdit,
    bool CanDelete
);


public record SavePharmacyRolePermissionsDto(
    [Required]
    List<PharmacyRolePermissionDto> Permissions
);


// ============================================================
// SUPER ADMIN - ASSIGN ROLE TO PHARMACY ADMIN
// ============================================================

public record AssignPharmacyRoleDto(
    int RoleId
);


// ============================================================
// PHARMACY ADMIN - SETTINGS SCREEN
// ============================================================

public record PharmacyAdminSettingsDto(
    [Required] string PharmacyName,
    string? PhoneNumber,
    string? EmailAddress,
    string? LicenseNumber,
    string? GSTNumber,
    string? Address,
    bool IsActive,
    [Required] string Currency,
    decimal TaxGSTRate,
    [Required] string InvoicePrefix,
    [Required] string PaymentTerms,
    [Required] string BusinessHours,
    int LowStockThreshold,
    int ExpiryAlertHorizonDays,
    bool StockManagementEnabled,
    bool BatchExpiryTrackingEnabled,
    decimal MaximumAllowedDiscount,
    [Required] string SalesInvoiceDetails,
    int ReturnWindowDays,
    bool AutomaticReorderingEnabled,
    bool LowStockNotificationsEnabled,
    bool ExpiryWarningAlertsEnabled,
    bool OrderTransferWarningsEnabled
);



// ============================================================
// STANDALONE PHARMACY - MANUAL PRESCRIPTION
// ============================================================

public sealed class CreateManualPharmacyPrescriptionDto
{
    [Required]
    public string PatientName { get; set; } = string.Empty;

    public string PatientPhone { get; set; } = string.Empty;

    public int PatientAge { get; set; }

    public string PatientGender { get; set; } = "Unknown";

    public string? DoctorName { get; set; }

    public string Diagnosis { get; set; } = string.Empty;

    public string Advice { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public List<ManualPharmacyPrescriptionItemDto> Medicines { get; set; } = new();
}


public sealed class ManualPharmacyPrescriptionItemDto
{
    public int MedicineId { get; set; }

    public int Quantity { get; set; }

    public int Days { get; set; } = 1;

    public string Dosage { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public bool Morning { get; set; }

    public bool Afternoon { get; set; }

    public bool Night { get; set; }
}