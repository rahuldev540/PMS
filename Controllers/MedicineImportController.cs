using System.Security.Claims;
using System.Text;
using AuthDemo.DTOs;
using AuthDemo.Data;
using Microsoft.EntityFrameworkCore;
using AuthDemo.Helpers;
using AuthDemo.Services.Interfaces;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/medicine-import")]
[Authorize(Roles = "PharmacyAdmin,Pharmacist")]
public class MedicineImportController : ControllerBase
{
    private const long MaximumUploadSize = 20_000_000;

    // =====================================================
    // MEDICINE IMPORT TEMPLATE - 22 SUGGESTED COLUMNS
    // These are column names only.
    // No medicine data is hardcoded in backend.
    // =====================================================

    private static readonly string[] RequiredColumns =
    {
        "MedicineCode",
        "Name",
        "GenericName",
        "BrandName",
        "Category",
        "DosageForm",
        "Strength",
        "Manufacturer",
        "PackSize",
        "SupplierCode",
        "SupplierName",
        "ContactPerson",
        "Phone",
        "Email",
        "BatchNumber",
        "ManufactureDate",
        "ExpiryDate",
        "StockQuantity",
        "PurchasePrice",
        "SellingPrice",
        "GST",
        "MinimumStock",
        "MaximumStock",
        "ReorderLevel",
        "StorageLocation"
    };

    private readonly IMedicineImportService _service;
    private readonly AppDbContext _db;
    private readonly PharmacyPermissionChecker _permissionChecker;

    public MedicineImportController(
        IMedicineImportService service,
        PharmacyPermissionChecker permissionChecker,
        AppDbContext db)
    {
        _service = service;
        _db = db;
        _permissionChecker = permissionChecker;
    }

    private int UserId =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id)
            ? id
            : 0;

    private async Task<IActionResult?> CheckMedicinePermission(
        string action)
    {
        if (await _permissionChecker.HasPermissionAsync(
                UserId,
                "Medicines",
                action))
        {
            return null;
        }

        return StatusCode(
            StatusCodes.Status403Forbidden,
            new
            {
                Success = false,
                Message =
                    $"You do not have {action} permission for Medicines."
            });
    }

    // Import routes use pharmacy-scoped import IDs; resolve their destination branch.
    // Pharmacy Admin may operate on imports in any branch of their pharmacy.
    // Pharmacists are limited to their own assigned branch.
    private async Task<int?> AuthorizedImportBranchAsync(int pharmacyId, int importId, int userId)
    {
        var internalImportId = await _db.PharmacyMedicineImportIdentities.AsNoTracking()
            .Where(i => i.PharmacyId == pharmacyId && i.ImportId == importId)
            .Select(i => (int?)i.ExistingImportId).SingleOrDefaultAsync();
        if (!internalImportId.HasValue) return null;
        var importBranchId = await _db.MedicineImports.AsNoTracking()
            .Where(i => i.Id == internalImportId.Value && i.PharmacyId == pharmacyId)
            .Select(i => (int?)i.BranchId).SingleOrDefaultAsync();
        if (!importBranchId.HasValue) return null;
        if (User.IsInRole("PharmacyAdmin")) return importBranchId;
        var assigned = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.PharmacyId == pharmacyId && u.IsActive && u.Role == "Pharmacist")
            .Select(u => u.BranchId).SingleOrDefaultAsync();
        return assigned == importBranchId ? importBranchId : null;
    }

    // =====================================================
    // DOWNLOAD EMPTY MEDICINE IMPORT TEMPLATE
    //
    // CSV:
    // GET /api/medicine-import/template?format=csv
    //
    // XLSX:
    // GET /api/medicine-import/template?format=xlsx
    //
    // Template contains ONLY 22 column headers.
    // No medicine records are hardcoded.
    // =====================================================

    [HttpGet("template")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DownloadTemplate(
        [FromQuery] string format = "xlsx")
    {
        var permissionError =
            await CheckMedicinePermission("View");

        if (permissionError != null)
            return permissionError;

        format =
            (format ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        // ---------------------------------------------
        // CSV TEMPLATE
        // ---------------------------------------------

        if (format == "csv")
        {
            // Header row only.
            var csv =
                string.Join(",", RequiredColumns)
                + "\r\n";

            var bytes =
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: true)
                .GetBytes(csv);

            return File(
                bytes,
                "text/csv",
                "medicine-import-template.csv");
        }

        // ---------------------------------------------
        // XLSX TEMPLATE
        // ---------------------------------------------

        if (format == "xlsx")
        {
            using var workbook =
                new XLWorkbook();

            var worksheet =
                workbook.Worksheets.Add(
                    "Medicine Upload");

            for (var columnIndex = 0;
                 columnIndex < RequiredColumns.Length;
                 columnIndex++)
            {
                worksheet
                    .Cell(1, columnIndex + 1)
                    .Value =
                    RequiredColumns[columnIndex];
            }

            // Header formatting only.
            worksheet.Row(1).Style.Font.Bold = true;

            worksheet
                .Row(1)
                .Style
                .Alignment
                .Horizontal =
                XLAlignmentHorizontalValues.Center;

            worksheet.SheetView.FreezeRows(1);

            worksheet.Columns().AdjustToContents();

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "medicine-import-template.xlsx");
        }

        return BadRequest(new
        {
            Success = false,
            Message =
                "Invalid template format. Supported formats are csv and xlsx."
        });
    }

    // =====================================================
    // UPLOAD AND VALIDATE CSV / XLSX
    //
    // POST /api/medicine-import/validate
    //
    // Frontend sends:
    // multipart/form-data
    // File = actual selected .csv or .xlsx
    //
    // This endpoint DOES NOT directly modify:
    // - Medicines
    // - Suppliers
    // - Inventory
    // - MedicineBatches
    //
    // Service performs schema + row validation and
    // creates staging import records.
    // =====================================================

    [HttpPost("validate")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumUploadSize)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = MaximumUploadSize)]
    // Internal processing only; not a public API endpoint.
    [NonAction]
    private async Task<IActionResult> Validate(
        [FromForm] MedicineImportUploadDto request)
    {
        var permissionError =
            await CheckMedicinePermission("Create");

        if (permissionError != null)
            return permissionError;

        // ---------------------------------------------
        // FILE REQUIRED
        // ---------------------------------------------

        if (request.File == null ||
            request.File.Length == 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A non-empty CSV or XLSX medicine file is required."
            });
        }

        // ---------------------------------------------
        // FILE SIZE
        // ---------------------------------------------

        if (request.File.Length >
            MaximumUploadSize)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "The uploaded file cannot exceed 20 MB."
            });
        }

        // ---------------------------------------------
        // FILE NAME
        // ---------------------------------------------

        var originalFileName =
            Path.GetFileName(
                request.File.FileName);

        if (string.IsNullOrWhiteSpace(
            originalFileName))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "The uploaded file name is invalid."
            });
        }

        // ---------------------------------------------
        // FILE EXTENSION
        // ---------------------------------------------

        var extension =
            Path.GetExtension(
                    originalFileName)
                .ToLowerInvariant();

        if (extension != ".csv" &&
            extension != ".xlsx")
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Only .csv and .xlsx medicine files are supported."
            });
        }

        // ---------------------------------------------
        // OPTIONAL CONTENT-TYPE CHECK
        //
        // Extension is still the primary check because
        // browsers can send different MIME types.
        // ---------------------------------------------

        var contentType =
            request.File.ContentType
            ?.Trim()
            .ToLowerInvariant();

        if (extension == ".csv")
        {
            var allowedCsvTypes =
                new[]
                {
                    "text/csv",
                    "application/csv",
                    "text/plain",
                    "application/vnd.ms-excel",
                    "application/octet-stream"
                };

            if (!string.IsNullOrWhiteSpace(
                    contentType) &&
                !allowedCsvTypes.Contains(
                    contentType,
                    StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "The selected file does not appear to be a valid CSV file."
                });
            }
        }

        if (extension == ".xlsx")
        {
            var allowedExcelTypes =
                new[]
                {
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "application/octet-stream"
                };

            if (!string.IsNullOrWhiteSpace(
                    contentType) &&
                !allowedExcelTypes.Contains(
                    contentType,
                    StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "The selected file does not appear to be a valid XLSX file."
                });
            }
        }

        // ---------------------------------------------
        // JWT CLAIMS
        // ---------------------------------------------

        if (!TryGetRequiredClaims(
                out var pharmacyId,
                out var branchId,
                out var userId,
                out var claimError))
        {
            return Unauthorized(new
            {
                Success = false,
                Message = claimError
            });
        }

        // Resolve pharmacy-visible BranchId to internal FK, never trust a raw branch ID.
        if (User.IsInRole("PharmacyAdmin"))
        {
            if (request.BranchId.HasValue)
            {
                var selected = await _db.PharmacyBranchIdentities.AsNoTracking()
                    .Where(i => i.PharmacyId == pharmacyId && i.BranchId == request.BranchId.Value)
                    .Select(i => (int?)i.ExistingBranchId).SingleOrDefaultAsync();
                if (!selected.HasValue || !await _db.Branches.AnyAsync(b => b.Id == selected.Value && b.PharmacyId == pharmacyId && b.IsActive))
                    return BadRequest(new { Success = false, Message = "Selected branch is invalid or not in your pharmacy." });
                branchId = selected.Value;
            }
            else if (branchId <= 0)
                return BadRequest(new { Success = false, Message = "BranchId is required for an unassigned Pharmacy Admin." });
        }
        else if (User.IsInRole("Pharmacist"))
        {
            var assigned = await _db.Users.AsNoTracking().Where(u => u.Id == userId && u.PharmacyId == pharmacyId && u.IsActive)
                .Select(u => u.BranchId).FirstOrDefaultAsync();
            if (!assigned.HasValue || assigned.Value <= 0)
                return StatusCode(403, new { Success = false, Message = "No active assigned branch." });
            if (request.BranchId.HasValue)
            {
                var selected = await _db.PharmacyBranchIdentities.AsNoTracking()
                    .Where(i => i.PharmacyId == pharmacyId && i.BranchId == request.BranchId.Value)
                    .Select(i => (int?)i.ExistingBranchId).SingleOrDefaultAsync();
                if (selected != assigned)
                    return StatusCode(403, new { Success = false, Message = "Pharmacists may import only into their assigned branch." });
            }
            branchId = assigned.Value;
        }
        if (!await _db.Branches.AnyAsync(b => b.Id == branchId && b.PharmacyId == pharmacyId && b.IsActive))
            return BadRequest(new { Success = false, Message = "Destination branch is not active in your pharmacy." });

        // ---------------------------------------------
        // SEND ACTUAL FRONTEND-UPLOADED FILE
        // TO IMPORT SERVICE
        //
        // Service must:
        // 1. Detect CSV/XLSX
        // 2. Read headers
        // 3. Map supported column aliases and validate required medicine name
        // 4. Read actual medicine rows
        // 5. Validate data types
        // 6. Validate business rules
        // 7. Check existing DB data
        // 8. Stage import rows
        // ---------------------------------------------

        try
        {
            var result =
                await _service.ValidateAsync(
                    request.File,
                    pharmacyId,
                    branchId,
                    userId);

            return Ok(new
            {
                Success = true,
                Message =
                    "Medicine file validation completed.",
                Data = result
            });
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new
            {
                Success = false,
                Message = ex.Message
            });
        }
        catch (FormatException ex)
        {
            return BadRequest(new
            {
                Success = false,
                Message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    // One-request CSV/XLSX import. Validation and commit are internal operations, not public endpoints.
    // Invalid rows are reported and valid rows are committed automatically.
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumUploadSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaximumUploadSize)]
    public async Task<IActionResult> Upload([FromForm] MedicineImportUploadDto request)
    {
        if (!User.IsInRole("PharmacyAdmin"))
            return StatusCode(403, new { Success = false, Message = "Only Pharmacy Admin can upload medicine files." });
        // Reuse all existing file, permission, JWT and branch authorization checks.
        var validation = await Validate(request);
        if (validation is not OkObjectResult ok || ok.Value == null)
            return validation;

        using var json = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(ok.Value));
        var data = json.RootElement.GetProperty("Data");
        var importId = data.GetProperty("ImportId").GetInt32();
        var validRows = data.GetProperty("ValidRows").GetInt32();
        var invalidRows = data.GetProperty("InvalidRows").GetInt32();
        if (invalidRows > 0 || validRows == 0)
            return BadRequest(new { Success = false, Message = "File validation failed. No medicines, inventory, stock or suppliers were imported. Correct the invalid rows and upload again.", ImportId = importId, ValidRows = validRows, InvalidRows = invalidRows });

        var options = new CommitMedicineImportDto
        {
            UpdateExistingMedicines = true,
            ImportSuppliers = true,
            ImportStock = true,
            SkipInvalidRows = false,
            DuplicateBatchAction = "Skip"
        };
        var result = await Commit(importId, options);
        if (result is not OkObjectResult committed)
            return result;

        return Ok(new
        {
            Success = true,
            Message = "Medicines, batches, stock, inventory and suppliers imported successfully for the selected branch.",
            ImportId = importId,
            ValidRows = validRows,
            InvalidRows = invalidRows,
            Data = committed.Value
        });
    }

    // =====================================================
    // COMMIT VALIDATED IMPORT
    //
    // POST /api/medicine-import/{importId}/commit
    //
    // Creates / updates actual:
    // - Medicines
    // - Suppliers
    // - Medicine batches
    // - Inventory
    //
    // =====================================================
    // COMMIT VALIDATED IMPORT
    // This is called only AFTER validation.
    // =====================================================

    // Internal processing only; not a public API endpoint.
    [NonAction]
    private async Task<IActionResult> Commit(
        int importId,
        [FromBody] CommitMedicineImportDto request)
    {
        var permissionError =
            await CheckMedicinePermission("Create");

        if (permissionError != null)
            return permissionError;

        if (importId <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message = "A valid import ID is required."
            });
        }

        if (request == null)
        {
            return BadRequest(new
            {
                Success = false,
                Message = "Import options are required."
            });
        }

        // Must match MedicineImportService.ValidateCommitOptions().
        var allowedDuplicateActions =
            new[]
            {
            "Error",
            "Skip",
            "AddQuantity"
            };

        var duplicateAction =
            allowedDuplicateActions
                .FirstOrDefault(x =>
                    x.Equals(
                        request.DuplicateBatchAction,
                        StringComparison.OrdinalIgnoreCase));

        if (duplicateAction == null)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "DuplicateBatchAction must be Error, Skip or AddQuantity."
            });
        }

        // Normalize the value before sending it to the service.
        request.DuplicateBatchAction =
            duplicateAction;

        if (!TryGetRequiredClaims(
                out var pharmacyId,
                out var branchId,
                out var userId,
                out var claimError))
        {
            return Unauthorized(new
            {
                Success = false,
                Message = claimError
            });
        }
        var importBranch = await AuthorizedImportBranchAsync(pharmacyId, importId, UserId);
        if (!importBranch.HasValue)
            return NotFound(new { Success = false, Message = "Import not found or not accessible for this branch." });
        branchId = importBranch.Value;


        var status =
            await _service.GetStatusAsync(
                importId,
                pharmacyId,
                branchId);

        if (status == null)
        {
            return NotFound(new
            {
                Success = false,
                Message = "Medicine import was not found."
            });
        }

        try
        {
            var result =
                await _service.CommitAsync(
                    importId,
                    request,
                    pharmacyId,
                    branchId,
                    userId);

            return Ok(new
            {
                Success = true,
                Message = "Medicine import completed.",
                Data = result
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                Success = false,
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    // =====================================================
    // PREVIEW / EDIT / REMOVE STAGED IMPORT ROWS
    // =====================================================

    [HttpPost("{importId:int}/rows")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateRow(
        int importId,
        [FromBody] CreateMedicineImportRowDto request)
    {
        var permissionError =
            await CheckMedicinePermission("Create");

        if (permissionError != null)
            return permissionError;

        if (importId <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message = "A valid import ID is required."
            });
        }

        if (request?.Data == null)
        {
            return BadRequest(new
            {
                Success = false,
                Message = "Medicine row data is required."
            });
        }

        if (!TryGetRequiredClaims(
                out var pharmacyId,
                out var branchId,
                out _,
                out var claimError))
        {
            return Unauthorized(new
            {
                Success = false,
                Message = claimError
            });
        }
        var importBranch = await AuthorizedImportBranchAsync(pharmacyId, importId, UserId);
        if (!importBranch.HasValue)
            return NotFound(new { Success = false, Message = "Import not found or not accessible for this branch." });
        branchId = importBranch.Value;


        try
        {
            var row = await _service.CreateRowAsync(
                importId,
                request,
                pharmacyId,
                branchId);

            return StatusCode(
                StatusCodes.Status201Created,
                new
                {
                    Success = true,
                    Message =
                        "Medicine import row added successfully.",
                    Data = row
                });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                Success = false,
                Message = ex.Message
            });
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new
            {
                Success = false,
                Message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    [HttpGet("{importId:int}/rows")]
    public async Task<IActionResult> GetRows(int importId)
    {
        var permissionError = await CheckMedicinePermission("View");
        if (permissionError != null)
            return permissionError;

        if (!TryGetRequiredClaims(
                out var pharmacyId,
                out var branchId,
                out _,
                out var claimError))
        {
            return Unauthorized(new { Success = false, Message = claimError });
        }
        var importBranch = await AuthorizedImportBranchAsync(pharmacyId, importId, UserId);
        if (!importBranch.HasValue)
            return NotFound(new { Success = false, Message = "Import not found or not accessible for this branch." });
        branchId = importBranch.Value;


        try
        {
            var rows = await _service.GetRowsAsync(
                importId,
                pharmacyId,
                branchId);

            return Ok(new { Success = true, Data = rows });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Success = false, Message = ex.Message });
        }
    }

    [HttpPut("{importId:int}/rows/{rowId:int}")]
    public async Task<IActionResult> UpdateRow(
        int importId,
        int rowId,
        [FromBody] UpdateMedicineImportRowDto request)
    {
        var permissionError = await CheckMedicinePermission("Edit");
        if (permissionError != null)
            return permissionError;

        if (!TryGetRequiredClaims(
                out var pharmacyId,
                out var branchId,
                out _,
                out var claimError))
        {
            return Unauthorized(new { Success = false, Message = claimError });
        }
        var importBranch = await AuthorizedImportBranchAsync(pharmacyId, importId, UserId);
        if (!importBranch.HasValue)
            return NotFound(new { Success = false, Message = "Import not found or not accessible for this branch." });
        branchId = importBranch.Value;


        try
        {
            var row = await _service.UpdateRowAsync(
                importId,
                rowId,
                request,
                pharmacyId,
                branchId);

            return Ok(new
            {
                Success = true,
                Message = "Import row updated successfully.",
                Data = row
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Success = false, Message = ex.Message });
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { Success = false, Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Success = false, Message = ex.Message });
        }
    }

    [HttpDelete("{importId:int}/rows/{rowId:int}")]
    public async Task<IActionResult> DeleteRow(
        int importId,
        int rowId)
    {
        var permissionError = await CheckMedicinePermission("Delete");
        if (permissionError != null)
            return permissionError;

        if (!TryGetRequiredClaims(
                out var pharmacyId,
                out var branchId,
                out _,
                out var claimError))
        {
            return Unauthorized(new { Success = false, Message = claimError });
        }
        var importBranch = await AuthorizedImportBranchAsync(pharmacyId, importId, UserId);
        if (!importBranch.HasValue)
            return NotFound(new { Success = false, Message = "Import not found or not accessible for this branch." });
        branchId = importBranch.Value;


        try
        {
            await _service.DeleteRowAsync(
                importId,
                rowId,
                pharmacyId,
                branchId);

            return Ok(new
            {
                Success = true,
                Message = "Import row removed successfully."
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Success = false, Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Success = false, Message = ex.Message });
        }
    }

    // =====================================================
    // GET IMPORT STATUS
    //
    // GET /api/medicine-import/{importId}/status
    // =====================================================

    [HttpGet("{importId:int}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Status(
        int importId)
    {
        var permissionError =
            await CheckMedicinePermission("View");

        if (permissionError != null)
            return permissionError;

        if (importId <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid import ID is required."
            });
        }

        if (!TryGetRequiredClaims(
                out var pharmacyId,
                out var branchId,
                out _,
                out var claimError))
        {
            return Unauthorized(new
            {
                Success = false,
                Message = claimError
            });
        }
        var importBranch = await AuthorizedImportBranchAsync(pharmacyId, importId, UserId);
        if (!importBranch.HasValue)
            return NotFound(new { Success = false, Message = "Import not found or not accessible for this branch." });
        branchId = importBranch.Value;


        var result =
            await _service.GetStatusAsync(
                importId,
                pharmacyId,
                branchId);

        if (result == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Medicine import was not found."
            });
        }

        return Ok(new
        {
            Success = true,
            Data = result
        });
    }

    // =====================================================
    // DOWNLOAD VALIDATION ERRORS
    //
    // GET /api/medicine-import/{importId}/errors
    //
    // Returns CSV containing invalid row information.
    // =====================================================

    [HttpGet("{importId:int}/errors")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Errors(
        int importId)
    {
        var permissionError =
            await CheckMedicinePermission("View");

        if (permissionError != null)
            return permissionError;

        if (importId <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid import ID is required."
            });
        }

        if (!TryGetRequiredClaims(
                out var pharmacyId,
                out var branchId,
                out _,
                out var claimError))
        {
            return Unauthorized(new
            {
                Success = false,
                Message = claimError
            });
        }
        var importBranch = await AuthorizedImportBranchAsync(pharmacyId, importId, UserId);
        if (!importBranch.HasValue)
            return NotFound(new { Success = false, Message = "Import not found or not accessible for this branch." });
        branchId = importBranch.Value;


        var status =
            await _service.GetStatusAsync(
                importId,
                pharmacyId,
                branchId);

        if (status == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Medicine import was not found."
            });
        }

        var file =
            await _service.GetErrorsAsync(
                importId,
                pharmacyId,
                branchId);

        return File(
            file,
            "text/csv",
            $"medicine-import-{importId}-errors.csv");
    }

    // =====================================================
    // JWT CLAIM VALIDATION
    // =====================================================

    private bool TryGetRequiredClaims(
        out int pharmacyId,
        out int branchId,
        out int userId,
        out string error)
    {
        pharmacyId = 0;
        branchId = 0;
        userId = 0;
        error = string.Empty;

        var pharmacyClaim =
            User.FindFirst(
                "PharmacyId")
            ?.Value;

        var branchClaim =
            User.FindFirst(
                "BranchId")
            ?.Value;

        var userClaim =
            User.FindFirst(
                ClaimTypes.NameIdentifier)
            ?.Value
            ?? User.FindFirst(
                "userId")
            ?.Value;

        if (!int.TryParse(
                pharmacyClaim,
                out pharmacyId) || pharmacyId <= 0)
        {
            error =
                "PharmacyId claim is missing or invalid.";

            return false;
        }

        if (!int.TryParse(
                branchClaim,
                out branchId))
        {
            error =
                "BranchId claim is missing or invalid.";

            return false;
        }

        if (!int.TryParse(
                userClaim,
                out userId))
        {
            error =
                "User ID claim is missing or invalid.";

            return false;
        }

        return true;
    }
}
