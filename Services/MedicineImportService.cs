using System.Globalization;
using System.Text;
using System.Text.Json;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Services.Interfaces;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Microsoft.VisualBasic.FileIO;

namespace AuthDemo.Services;

public class MedicineImportService : IMedicineImportService
{
    private readonly AppDbContext _db;

    /*
     * IMPORTANT:
     *
     * These are NOT medicine details.
     *
     * This is only the schema expected from the uploaded file.
     *
     * Actual values such as:
     *
     * MED-001
     * Paracetamol
     * 500 mg
     * BAT-001
     *
     * come from the file uploaded by the frontend.
     */
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

        "BatchNumber",
        "ManufactureDate",
        "ExpiryDate",

        "Quantity",
        "PurchasePrice",
        "SellingPrice",
        "GST",

        "MinimumStock",
        "MaximumStock",
        "ReorderLevel",

        "StorageLocation"
    };

    private static readonly string[] SupportedExtensions =
    {
        ".csv",
        ".xlsx"
    };

    // Common supplier files use different headings for the same field.
    // Unknown extra columns are accepted and ignored.
    private static readonly Dictionary<string, string[]> ColumnAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MedicineCode"] = ["MedicineCode", "Medicine Code", "Code", "ProductCode", "Product Code", "SKU", "Item Code", "Drug Code", "Product ID", "Item ID"],
            ["Name"] = ["Name", "MedicineName", "Medicine Name", "ProductName", "Product Name", "ItemName", "Item Name", "DrugName", "Drug Name", "Product Description", "Item Description", "Medicine Description", "Description"],
            ["GenericName"] = ["GenericName", "Generic Name", "Generic"],
            ["BrandName"] = ["BrandName", "Brand Name", "Brand"],
            ["Category"] = ["Category", "MedicineCategory", "Medicine Category"],
            ["DosageForm"] = ["DosageForm", "Dosage Form", "Form", "Medicine Form"],
            ["Strength"] = ["Strength", "Dose", "DosageStrength", "Dosage Strength"],
            ["Manufacturer"] = ["Manufacturer", "ManufacturerName", "Manufacturer Name", "Company", "Mfg Company", "Mfr", "Maker", "Manufactured By"],
            ["PackSize"] = ["PackSize", "Pack Size", "Packing", "Pack", "Package Size", "Unit", "UOM"],
            ["SupplierCode"] = ["SupplierCode", "Supplier Code", "VendorCode", "Vendor Code"],
            ["SupplierName"] = ["SupplierName", "Supplier Name", "Supplier", "Vendor"],
            ["SupplierContactPerson"] = ["SupplierContactPerson", "Supplier Contact Person", "ContactPerson", "Contact Person", "Supplier Contact", "Vendor Contact"],
            ["SupplierPhone"] = ["SupplierPhone", "Supplier Phone", "Supplier Mobile", "SupplierMobile", "Vendor Phone", "Vendor Mobile", "Phone", "Mobile"],
            ["SupplierEmail"] = ["SupplierEmail", "Supplier Email", "Vendor Email", "Email"],
            ["SupplierAddress"] = ["SupplierAddress", "Supplier Address", "Vendor Address", "Address"],
            ["SupplierCity"] = ["SupplierCity", "Supplier City", "Vendor City", "City"],
            ["SupplierState"] = ["SupplierState", "Supplier State", "Vendor State", "State"],
            ["SupplierTaxNumber"] = ["SupplierTaxNumber", "Supplier Tax Number", "TaxNumber", "Tax Number", "GSTIN", "GST No", "GST Number"],
            ["SupplierNotes"] = ["SupplierNotes", "Supplier Notes", "Vendor Notes", "Notes"],
            ["BatchNumber"] = ["BatchNumber", "Batch Number", "BatchNo", "Batch No", "Batch", "Lot", "Lot No", "Lot Number"],
            ["ManufactureDate"] = ["ManufactureDate", "Manufacture Date", "ManufacturingDate", "Manufacturing Date", "MFG Date", "MfgDate", "MFG", "Manufactured Date"],
            ["ExpiryDate"] = ["ExpiryDate", "Expiry Date", "ExpirationDate", "Expiration Date", "EXP Date", "ExpDate", "Expiry", "EXP", "Valid Till"],
            ["Quantity"] = ["Quantity", "Qty", "Stock", "OpeningStock", "Opening Stock", "Available Qty", "Available Quantity", "Current Stock", "Balance Qty", "Closing Stock", "StockQuantity", "Stock Quantity", "Stock_Quantity"],
            ["PurchasePrice"] = ["PurchasePrice", "Purchase Price", "CostPrice", "Cost Price", "Cost", "Purchase Rate", "Buy Price", "Buying Price", "PTR"],
            ["SellingPrice"] = ["SellingPrice", "Selling Price", "SalePrice", "Sale Price", "MRP", "Sale Rate", "Retail Price", "Unit Price", "Price"],
            ["GST"] = ["GST", "GSTPercentage", "GST Percentage", "Tax", "TaxPercentage"],
            ["MinimumStock"] = ["MinimumStock", "Minimum Stock", "MinStock", "Min Stock"],
            ["MaximumStock"] = ["MaximumStock", "Maximum Stock", "MaxStock", "Max Stock"],
            ["ReorderLevel"] = ["ReorderLevel", "Reorder Level", "ReorderStock", "Reorder Stock"],
            ["StorageLocation"] = ["StorageLocation", "Storage Location", "Location", "Rack"]
        };

    public MedicineImportService(AppDbContext db)
    {
        _db = db;
    }

    // Resolve a pharmacy-visible import number to the existing global FK.
    // Branch authorization remains enforced by each existing operation.
    private async Task<int> ResolveImportIdAsync(int pharmacyId, int importId)
    {
        var internalId = await _db.PharmacyMedicineImportIdentities
            .AsNoTracking()
            .Where(x => x.PharmacyId == pharmacyId && x.ImportId == importId)
            .Select(x => (int?)x.ExistingImportId)
            .SingleOrDefaultAsync();
        if (!internalId.HasValue)
            throw new KeyNotFoundException("Medicine import was not found for this pharmacy.");
        return internalId.Value;
    }

    // ============================================================
    // VALIDATE FILE
    // ============================================================

    public async Task<object> ValidateAsync(
        IFormFile file,
        int pharmacyId,
        int branchId,
        int userId)
    {
        ValidateBasicFile(file);
        if (pharmacyId <= 0 || branchId <= 0 || userId <= 0)
            throw new ArgumentException("A valid pharmacy, branch and user are required for a standalone pharmacy import.");

        var import = new MedicineImport
        {
            PharmacyId = pharmacyId,
            BranchId = branchId,
            FileName = Path.GetFileName(file.FileName),
            Status = "Validating",
            UploadedByUserId = userId
        };

        // Allocate one sequence per pharmacy (not per branch). The SQL Server
        // transaction application lock serializes concurrent uploads for this pharmacy.
        int publicImportId;
        await using (var identityTransaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DECLARE @result int; EXEC @result = sp_getapplock @Resource = {"MedicineImport-Pharmacy-" + pharmacyId}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {15000}; IF @result < 0 THROW 51000, 'Unable to allocate pharmacy import ID', 1;");
            publicImportId = (await _db.PharmacyMedicineImportIdentities
                .Where(x => x.PharmacyId == pharmacyId)
                .MaxAsync(x => (int?)x.ImportId) ?? 0) + 1;
            _db.MedicineImports.Add(import);
            await _db.SaveChangesAsync();
            _db.PharmacyMedicineImportIdentities.Add(new PharmacyMedicineImportIdentity
            {
                PharmacyId = pharmacyId,
                ImportId = publicImportId,
                ExistingImportId = import.Id
            });
            await _db.SaveChangesAsync();
            await identityTransaction.CommitAsync();
        }

        try
        {
            var extension =
                Path.GetExtension(file.FileName).ToLowerInvariant();

            List<UploadedMedicineRow> uploadedRows;

            if (extension == ".csv")
            {
                uploadedRows = await ReadCsvAsync(file);
            }
            else if (extension == ".xlsx")
            {
                uploadedRows = await ReadExcelAsync(file);
            }
            else
            {
                throw new Exception(
                    "Only CSV and XLSX files are supported.");
            }

            if (uploadedRows.Count == 0)
            {
                throw new InvalidDataException(
                    "The uploaded file does not contain any medicine rows.");
            }

            foreach (var uploadedRow in uploadedRows)
            {
                var importRow = new MedicineImportRow
                {
                    MedicineImportId = import.Id,
                    RowNumber = uploadedRow.RowNumber
                };

                try
                {
                    var data = ConvertToMedicineDto(uploadedRow);

                    ValidateRow(data);

                    await ValidateReferencesAsync(
                        data,
                        pharmacyId,
                        branchId);

                    var medicineExists =
                        await _db.Medicines
                            .AsNoTracking()
                            .AnyAsync(x =>
                                x.PharmacyId == pharmacyId &&
                                x.BranchId == branchId &&
                                x.MedicineCode == data.MedicineCode);

                    importRow.MedicineCode =
                        data.MedicineCode;

                    importRow.RawData =
                        JsonSerializer.Serialize(data);

                    importRow.Action =
                        medicineExists
                            ? "Update"
                            : "Create";

                    importRow.Status = "Valid";

                    import.ValidRows++;
                }
                catch (Exception ex)
                {
                    importRow.MedicineCode =
                        GetDictionaryValue(
                            uploadedRow.Values,
                            "MedicineCode");

                    importRow.RawData =
                        JsonSerializer.Serialize(
                            uploadedRow.Values);

                    // Action is required by the database and must never be null.
                    importRow.Action = "Invalid";
                    importRow.Status = "Invalid";
                    importRow.ErrorMessage =
                        ex.Message ?? "Medicine row validation failed.";

                    import.InvalidRows++;
                }

                import.TotalRows++;

                _db.MedicineImportRows.Add(importRow);
            }

            import.Status =
                import.InvalidRows == 0
                    ? "Validated"
                    : "ValidationFailed";

            await _db.SaveChangesAsync();

            // Return the pharmacy-specific BranchId, not the internal FK.
            var publicBranchId = await _db.PharmacyBranchIdentities
                .AsNoTracking()
                .Where(x => x.PharmacyId == pharmacyId && x.ExistingBranchId == branchId)
                .Select(x => (int?)x.BranchId)
                .SingleOrDefaultAsync();
            if (!publicBranchId.HasValue)
                throw new InvalidOperationException("Destination branch mapping was not found.");

            return new
            {
                PharmacyId = pharmacyId,
                BranchId = publicBranchId.Value,
                ImportId = publicImportId,
                import.FileName,
                import.TotalRows,
                import.ValidRows,
                import.InvalidRows,

                CanImport = import.ValidRows > 0,

                FullyValid = import.InvalidRows == 0,

                import.Status
            };
        }
        catch
        {
            import.Status = "Failed";

            await _db.SaveChangesAsync();

            throw;
        }
    }

    // ============================================================
    // COMMIT VALIDATED IMPORT
    // ============================================================

    public async Task<object> CommitAsync(
        int importId,
        CommitMedicineImportDto dto,
        int pharmacyId,
        int branchId,
        int userId)
    {
        var internalImportId = await ResolveImportIdAsync(pharmacyId, importId);
        var import =
            await _db.MedicineImports
                .Include(x => x.Rows)
                .FirstOrDefaultAsync(x =>
                    x.Id == internalImportId &&
                    x.PharmacyId == pharmacyId &&
                    x.BranchId == branchId);

        if (import == null)
            throw new Exception(
                "Medicine import was not found.");

        if (import.Status == "Completed" ||
            import.Status == "PartiallyCompleted")
        {
            throw new Exception(
                "This import has already been processed.");
        }

        if (import.ValidRows == 0)
        {
            throw new Exception(
                "There are no valid medicine rows to import.");
        }

        if (import.InvalidRows > 0 &&
            !dto.SkipInvalidRows)
        {
            throw new Exception(
                "Import contains invalid rows. " +
                "Correct the uploaded file or enable SkipInvalidRows.");
        }

        ValidateCommitOptions(dto);

        import.Status = "Processing";
        import.StartedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var validRows =
                import.Rows
                    .Where(x => x.Status == "Valid")
                    .OrderBy(x => x.RowNumber)
                    .ToList();

            foreach (var row in validRows)
            {
                var data =
                    JsonSerializer
                        .Deserialize<MedicineImportDataDto>(
                            row.RawData);

                if (data == null)
                {
                    throw new Exception(
                        $"Could not deserialize row {row.RowNumber}.");
                }

                /*
                 * Supplier
                 */
                Supplier? supplier = null;
                var hasSupplierData =
                    !string.IsNullOrWhiteSpace(data.SupplierCode) ||
                    !string.IsNullOrWhiteSpace(data.SupplierName);

                if (hasSupplierData)
                {
                    supplier = await UpsertSupplierAsync(
                        data,
                        pharmacyId,
                        branchId,
                        import,
                        dto.ImportSuppliers);
                }

                /*
                 * Medicine
                 */
                var medicine =
                    await UpsertMedicineAsync(
                        data,
                        pharmacyId,
                        branchId,
                        import,
                        dto.UpdateExistingMedicines);

                /*
                 * Important:
                 *
                 * Save Medicine/Supplier before Inventory because
                 * Medicine.Id may be database-generated.
                 */
                await _db.SaveChangesAsync();

                row.MedicineId = medicine.Id;
                row.SupplierId = supplier?.Id;

                /*
                 * Inventory / batch / transaction
                 */
                var hasStockData =
                    dto.ImportStock &&
                    !string.IsNullOrWhiteSpace(data.BatchNumber) &&
                    data.Quantity > 0 &&
                    data.ManufactureDate != default &&
                    data.ExpiryDate != default;

                if (hasStockData)
                {
                    await ImportStockAsync(
                        data,
                        medicine,
                        supplier,
                        import,
                        row,
                        pharmacyId,
                        branchId,
                        userId,
                        dto.DuplicateBatchAction);
                }

                row.Status = "Imported";
                row.ProcessedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
            }

            import.Status =
                import.InvalidRows > 0
                    ? "PartiallyCompleted"
                    : "Completed";

            import.CompletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return new
            {
                ImportId = importId,
                import.Status,

                import.TotalRows,
                import.ValidRows,
                import.InvalidRows,

                import.CreatedMedicines,
                import.UpdatedMedicines,
                import.CreatedSuppliers,
                import.CreatedBatches,
                import.StockUnitsAdded
            };
        }
        catch
        {
            await transaction.RollbackAsync();

            /*
             * After rollback, mark import as failed.
             */
            import.Status = "Failed";
            import.CompletedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch
            {
                // Don't hide original exception.
            }

            throw;
        }
    }

    // ============================================================
    // GET IMPORT STATUS
    // ============================================================

    public async Task<object?> GetStatusAsync(
        int importId,
        int pharmacyId,
        int branchId)
    {
        var internalImportId = await ResolveImportIdAsync(pharmacyId, importId);
        return await _db.MedicineImports
            .AsNoTracking()
            .Where(x =>
                x.Id == internalImportId &&
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId)
            .Select(x => new
            {
                ImportId = importId,

                x.FileName,
                x.Status,

                x.TotalRows,
                x.ValidRows,
                x.InvalidRows,

                x.CreatedMedicines,
                x.UpdatedMedicines,
                x.CreatedSuppliers,
                x.CreatedBatches,
                x.StockUnitsAdded,

                x.CreatedAt,
                x.StartedAt,
                x.CompletedAt
            })
            .FirstOrDefaultAsync();
    }

    // ============================================================
    // GET VALIDATION ERRORS CSV
    // ============================================================

    public async Task<byte[]> GetErrorsAsync(
        int importId,
        int pharmacyId,
        int branchId)
    {
        var internalImportId = await ResolveImportIdAsync(pharmacyId, importId);
        var rows =
            await _db.MedicineImportRows
                .AsNoTracking()
                .Where(x =>
                    x.MedicineImportId == internalImportId &&
                    x.MedicineImport.PharmacyId == pharmacyId &&
                    x.MedicineImport.BranchId == branchId &&
                    x.Status == "Invalid")
                .OrderBy(x => x.RowNumber)
                .ToListAsync();

        var csv = new StringBuilder();

        csv.AppendLine(
            "RowNumber,MedicineCode,ErrorMessage");

        foreach (var row in rows)
        {
            csv.AppendLine(
                $"{row.RowNumber}," +
                $"{EscapeCsv(row.MedicineCode ?? string.Empty)}," +
                $"{EscapeCsv(row.ErrorMessage ?? string.Empty)}");
        }

        return Encoding.UTF8.GetBytes(
            csv.ToString());
    }

    // ============================================================
    // STAGED ROW PREVIEW / EDIT / DELETE
    // ============================================================

    public async Task<MedicineImportRowDto> CreateRowAsync(
        int importId,
        CreateMedicineImportRowDto dto,
        int pharmacyId,
        int branchId)
    {
        var internalImportId = await ResolveImportIdAsync(pharmacyId, importId);
        var import = await _db.MedicineImports
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x =>
                x.Id == internalImportId &&
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId);

        EnsureImportCanBeEdited(import);

        if (dto?.Data == null)
        {
            throw new InvalidDataException(
                "Medicine row data is required.");
        }

        var data = dto.Data;

        if (string.IsNullOrWhiteSpace(data.Name))
        {
            throw new InvalidDataException(
                "Medicine name is required.");
        }

        data.Name = data.Name.Trim();

        if (string.IsNullOrWhiteSpace(data.MedicineCode))
        {
            data.MedicineCode =
                $"MED-{Guid.NewGuid():N}"[..20]
                    .ToUpperInvariant();
        }
        else
        {
            data.MedicineCode =
                data.MedicineCode.Trim();
        }

        ValidateRow(data);

        await ValidateReferencesAsync(
            data,
            pharmacyId,
            branchId);

        var duplicateStagedCode = import!.Rows.Any(row =>
            row.MedicineCode.Equals(
                data.MedicineCode,
                StringComparison.OrdinalIgnoreCase));

        if (duplicateStagedCode)
        {
            throw new InvalidOperationException(
                $"Medicine code '{data.MedicineCode}' " +
                "already exists in this import.");
        }

        var medicineExists = await _db.Medicines
            .AsNoTracking()
            .AnyAsync(x =>
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId &&
                x.MedicineCode == data.MedicineCode);

        var nextRowNumber = import.Rows.Count == 0
            ? 2
            : import.Rows.Max(x => x.RowNumber) + 1;

        var row = new MedicineImportRow
        {
            MedicineImportId = internalImportId,
            RowNumber = nextRowNumber,
            MedicineCode = data.MedicineCode,
            RawData = JsonSerializer.Serialize(data),
            Action = medicineExists
                ? "Update"
                : "Create",
            Status = "Valid",
            ErrorMessage = string.Empty
        };

        _db.MedicineImportRows.Add(row);
        import.Rows.Add(row);

        RecalculateImportTotals(import);

        await _db.SaveChangesAsync();

        return ToRowDto(row);
    }

    public async Task<List<MedicineImportRowDto>> GetRowsAsync(
        int importId,
        int pharmacyId,
        int branchId)
    {
        var internalImportId = await ResolveImportIdAsync(pharmacyId, importId);
        var importExists = await _db.MedicineImports
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == internalImportId &&
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId);

        if (!importExists)
            throw new KeyNotFoundException("Medicine import was not found.");

        var rows = await _db.MedicineImportRows
            .AsNoTracking()
            .Where(x => x.MedicineImportId == internalImportId)
            .OrderBy(x => x.RowNumber)
            .ToListAsync();

        return rows.Select(ToRowDto).ToList();
    }

    public async Task<MedicineImportRowDto> UpdateRowAsync(
        int importId,
        int rowId,
        UpdateMedicineImportRowDto dto,
        int pharmacyId,
        int branchId)
    {
        var internalImportId = await ResolveImportIdAsync(pharmacyId, importId);
        var import = await _db.MedicineImports
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x =>
                x.Id == internalImportId &&
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId);

        EnsureImportCanBeEdited(import);

        var row = import!.Rows.FirstOrDefault(x => x.Id == rowId);
        if (row == null)
            throw new KeyNotFoundException("Medicine import row was not found.");

        var data = dto?.Data
            ?? throw new InvalidDataException("Medicine row data is required.");

        if (string.IsNullOrWhiteSpace(data.MedicineCode))
        {
            data.MedicineCode =
                $"MED-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        }

        ValidateRow(data);
        await ValidateReferencesAsync(data, pharmacyId, branchId);

        var medicineExists = await _db.Medicines
            .AsNoTracking()
            .AnyAsync(x =>
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId &&
                x.MedicineCode == data.MedicineCode);

        row.MedicineCode = data.MedicineCode;
        row.RawData = JsonSerializer.Serialize(data);
        row.Action = medicineExists ? "Update" : "Create";
        row.Status = "Valid";
        row.ErrorMessage = string.Empty;

        RecalculateImportTotals(import);
        await _db.SaveChangesAsync();

        return ToRowDto(row);
    }

    public async Task DeleteRowAsync(
        int importId,
        int rowId,
        int pharmacyId,
        int branchId)
    {
        var internalImportId = await ResolveImportIdAsync(pharmacyId, importId);
        var import = await _db.MedicineImports
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x =>
                x.Id == internalImportId &&
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId);

        EnsureImportCanBeEdited(import);

        var row = import!.Rows.FirstOrDefault(x => x.Id == rowId);
        if (row == null)
            throw new KeyNotFoundException("Medicine import row was not found.");

        _db.MedicineImportRows.Remove(row);
        import.Rows.Remove(row);
        RecalculateImportTotals(import);
        await _db.SaveChangesAsync();
    }

    private static MedicineImportRowDto ToRowDto(MedicineImportRow row)
    {
        MedicineImportDataDto data;

        try
        {
            data = JsonSerializer.Deserialize<MedicineImportDataDto>(
                row.RawData) ?? new MedicineImportDataDto();
        }
        catch (JsonException)
        {
            data = new MedicineImportDataDto
            {
                MedicineCode = row.MedicineCode
            };
        }

        return new MedicineImportRowDto
        {
            Id = row.Id,
            RowNumber = row.RowNumber,
            MedicineCode = row.MedicineCode,
            Data = data,
            Action = row.Action,
            Status = row.Status,
            ErrorMessage = row.ErrorMessage
        };
    }

    private static void EnsureImportCanBeEdited(MedicineImport? import)
    {
        if (import == null)
            throw new KeyNotFoundException("Medicine import was not found.");

        if (import.Status is "Processing" or "Completed" or
            "PartiallyCompleted" or "Cancelled")
        {
            throw new InvalidOperationException(
                "This medicine import can no longer be edited.");
        }
    }

    private static void RecalculateImportTotals(MedicineImport import)
    {
        import.TotalRows = import.Rows.Count;
        import.ValidRows = import.Rows.Count(x => x.Status == "Valid");
        import.InvalidRows = import.Rows.Count(x => x.Status == "Invalid");
        import.Status = import.InvalidRows == 0
            ? "Validated"
            : "ValidationFailed";
    }

    // ============================================================
    // FILE VALIDATION
    // ============================================================

    private static void ValidateBasicFile(
        IFormFile file)
    {
        if (file == null)
        {
            throw new Exception(
                "No medicine file was uploaded.");
        }

        if (file.Length == 0)
        {
            throw new Exception(
                "Uploaded medicine file is empty.");
        }

        var extension =
            Path.GetExtension(file.FileName)
                .ToLowerInvariant();

        if (!SupportedExtensions.Contains(extension))
        {
            throw new Exception(
                "Invalid file type. Only .csv and .xlsx files are supported.");
        }

        /*
         * Example 10 MB limit.
         * Change depending on your requirement.
         */
        const long maxFileSize =
            20 * 1024 * 1024;

        if (file.Length > maxFileSize)
        {
            throw new Exception(
                "Medicine file cannot exceed 20 MB.");
        }
    }

    // ============================================================
    // READ CSV
    // ============================================================

    private static async Task<List<UploadedMedicineRow>>
        ReadCsvAsync(
            IFormFile file)
    {
        var result =
            new List<UploadedMedicineRow>();

        await using var stream =
            file.OpenReadStream();

        using var reader =
            new StreamReader(stream);

        using var parser =
            new TextFieldParser(reader);

        parser.TextFieldType =
            FieldType.Delimited;

        parser.SetDelimiters(",");

        parser.HasFieldsEnclosedInQuotes =
            true;

        parser.TrimWhiteSpace =
            true;

        if (parser.EndOfData)
        {
            throw new InvalidDataException(
                "CSV file is empty.");
        }

        var headers =
            parser.ReadFields();

        if (headers == null ||
            headers.Length == 0)
        {
            throw new InvalidDataException(
                "CSV header could not be read.");
        }

        var normalizedHeaders =
            ValidateAndNormalizeHeaders(headers);

        var rowNumber = 1;

        while (!parser.EndOfData)
        {
            rowNumber++;

            string[] values;

            try
            {
                values =
                    parser.ReadFields()
                    ?? Array.Empty<string>();

                // Flexible CSV import: missing trailing values are treated as empty
                // and values beyond the known header count are ignored.
            }
            catch (MalformedLineException ex)
            {
                throw new InvalidDataException(
                    $"CSV row {rowNumber} is malformed: {ex.Message}");
            }

            /*
             * Completely blank rows are ignored.
             */
            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var dictionary =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            for (var i = 0;
                 i < normalizedHeaders.Length;
                 i++)
            {
                dictionary[normalizedHeaders[i]] =
                    i < values.Length
                        ? values[i]?.Trim() ?? string.Empty
                        : string.Empty;
            }

            result.Add(
                new UploadedMedicineRow
                {
                    RowNumber = rowNumber,
                    Values = dictionary
                });
        }

        return result;
    }

    // ============================================================
    // READ EXCEL
    // ============================================================

    private static async Task<List<UploadedMedicineRow>>
        ReadExcelAsync(
            IFormFile file)
    {
        var result =
            new List<UploadedMedicineRow>();

        await using var stream =
            new MemoryStream();

        await file.CopyToAsync(stream);

        stream.Position = 0;

        using var workbook =
            new XLWorkbook(stream);

        var worksheet =
            workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
        {
            throw new InvalidDataException(
                "Excel file does not contain a worksheet.");
        }

        var usedRange =
            worksheet.RangeUsed();

        if (usedRange == null)
        {
            throw new InvalidDataException(
                "Excel file is empty.");
        }

        // Supplier/customer workbooks sometimes contain a title, company name,
        // report date, or blank rows before the real column headings. Scan the
        // first few used rows and choose the row that best matches medicine fields.
        var headerRowNumber = FindBestExcelHeaderRow(usedRange);
        var firstRow = worksheet.Row(headerRowNumber);
        var firstColumnNumber = usedRange.FirstColumn().ColumnNumber();
        var lastColumnNumber = usedRange.LastColumn().ColumnNumber();

        var headers = Enumerable
            .Range(firstColumnNumber, lastColumnNumber - firstColumnNumber + 1)
            .Select(columnNumber => firstRow.Cell(columnNumber).GetString().Trim())
            .ToArray();

        var normalizedHeaders = ValidateAndNormalizeHeaders(headers);

        var firstDataRowNumber = headerRowNumber + 1;

        var lastRowNumber =
            usedRange.LastRow()
                .RowNumber();

        for (var rowNumber = firstDataRowNumber;
             rowNumber <= lastRowNumber;
             rowNumber++)
        {
            var row =
                worksheet.Row(rowNumber);

            var dictionary =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            var hasValue = false;

            for (var columnIndex = 0;
                 columnIndex < normalizedHeaders.Length;
                 columnIndex++)
            {
                var cell =
                    row.Cell(firstColumnNumber + columnIndex);

                var value =
                    GetExcelCellValue(cell);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    hasValue = true;
                }

                dictionary[
                    normalizedHeaders[columnIndex]] =
                    value;
            }

            if (!hasValue)
            {
                continue;
            }

            result.Add(
                new UploadedMedicineRow
                {
                    RowNumber = rowNumber,
                    Values = dictionary
                });
        }

        return result;
    }

    // ============================================================
    // HEADER / SCHEMA VALIDATION
    // ============================================================

    private static string[]
        ValidateAndNormalizeHeaders(
            IEnumerable<string> headers)
    {
        var uploadedHeaders =
            headers
                .Select(x =>
                    (x ?? string.Empty).Trim())
                .ToArray();

        if (uploadedHeaders.Length == 0)
        {
            throw new InvalidDataException(
                "The uploaded medicine file has no column headers.");
        }

        // Blank headings are common in supplier sheets. Keep their column
        // position but give them an ignored synthetic name instead of failing.
        for (var i = 0; i < uploadedHeaders.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(uploadedHeaders[i]))
                uploadedHeaders[i] = $"__IgnoredColumn{i + 1}";
        }

        var mappedHeaders = uploadedHeaders
            .Select(header =>
                ResolveCanonicalColumn(header) ?? header)
            .ToArray();

        var duplicateMappings = mappedHeaders
            .Where(x => RequiredColumns.Contains(
                x,
                StringComparer.OrdinalIgnoreCase))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicateMappings.Count > 0)
        {
            throw new InvalidDataException(
                "Multiple uploaded columns map to the same medicine field: " +
                string.Join(", ", duplicateMappings));
        }

        if (!mappedHeaders.Contains(
                "Name",
                StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "A medicine name column is required. " +
                "Supported headings include Name, Medicine Name, Product Name and Item Name.");
        }

        return mappedHeaders;
    }

    private static string NormalizeHeader(string value)
    {
        return new string(
            (value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }

    private static string? ResolveCanonicalColumn(string uploadedHeader)
    {
        var normalizedUploaded = NormalizeHeader(uploadedHeader);
        if (string.IsNullOrWhiteSpace(normalizedUploaded) || normalizedUploaded.StartsWith("ignoredcolumn"))
            return null;

        // 1) Exact normalized alias match: spaces, underscores, hyphens and case do not matter.
        foreach (var mapping in ColumnAliases)
        {
            if (mapping.Value.Any(alias => NormalizeHeader(alias) == normalizedUploaded))
                return mapping.Key;
        }

        // 2) Conservative fuzzy match for small spelling variations such as
        // "medicin name", "manufaturer" or "expirydt". We intentionally do
        // not guess when the match is ambiguous. Unknown columns are ignored.
        var candidates = new List<(string Canonical, int Distance, int AliasLength)>();
        foreach (var mapping in ColumnAliases)
        {
            foreach (var alias in mapping.Value)
            {
                var normalizedAlias = NormalizeHeader(alias);
                if (normalizedAlias.Length < 4 || normalizedUploaded.Length < 4)
                    continue;

                var distance = LevenshteinDistance(normalizedUploaded, normalizedAlias);
                var maxLength = Math.Max(normalizedUploaded.Length, normalizedAlias.Length);
                var allowed = maxLength <= 7 ? 1 : maxLength <= 13 ? 2 : 3;
                if (distance <= allowed)
                    candidates.Add((mapping.Key, distance, normalizedAlias.Length));
            }
        }

        var ranked = candidates
            .GroupBy(x => x.Canonical, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(x => x.Distance).ThenByDescending(x => x.AliasLength).First())
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.AliasLength)
            .ToList();

        if (ranked.Count == 0) return null;
        if (ranked.Count > 1 && ranked[0].Distance == ranked[1].Distance) return null;
        return ranked[0].Canonical;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var j = 0; j <= right.Length; j++) previous[j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }

    private static int FindBestExcelHeaderRow(IXLRange usedRange)
    {
        var first = usedRange.FirstRow().RowNumber();
        var last = Math.Min(usedRange.LastRow().RowNumber(), first + 14);
        var firstColumn = usedRange.FirstColumn().ColumnNumber();
        var lastColumn = usedRange.LastColumn().ColumnNumber();

        var bestRow = first;
        var bestScore = -1;
        for (var rowNumber = first; rowNumber <= last; rowNumber++)
        {
            var canonical = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var col = firstColumn; col <= lastColumn; col++)
            {
                var header = usedRange.Worksheet.Row(rowNumber).Cell(col).GetString().Trim();
                var mapped = ResolveCanonicalColumn(header);
                if (mapped != null) canonical.Add(mapped);
            }

            // Name is the only required medicine field, so strongly prefer rows
            // where it can be recognized; then prefer the row with most matches.
            var score = canonical.Count + (canonical.Contains("Name") ? 100 : 0);
            if (score > bestScore)
            {
                bestScore = score;
                bestRow = rowNumber;
            }
        }
        return bestRow;
    }

    // ============================================================
    // CONVERT UPLOADED ROW -> DTO
    // ============================================================

    private static MedicineImportDataDto
        ConvertToMedicineDto(
            UploadedMedicineRow row)
    {
        string Value(string column)
        {
            return GetDictionaryValue(
                row.Values,
                column);
        }

        var medicineCode = Value("MedicineCode");

        if (string.IsNullOrWhiteSpace(medicineCode))
        {
            medicineCode =
                $"MED-{Guid.NewGuid():N}"[..20]
                    .ToUpperInvariant();
        }

        return new MedicineImportDataDto
        {
            MedicineCode =
                medicineCode,

            Name =
                Value("Name"),

            GenericName =
                Value("GenericName"),

            BrandName =
                Value("BrandName"),

            Category =
                Value("Category"),

            DosageForm =
                Value("DosageForm"),

            Strength =
                Value("Strength"),

            Manufacturer =
                Value("Manufacturer"),

            PackSize =
                Value("PackSize"),

            SupplierCode =
                Value("SupplierCode"),

            SupplierName =
                Value("SupplierName"),

            SupplierContactPerson =
                Value("SupplierContactPerson"),

            SupplierPhone =
                Value("SupplierPhone"),

            SupplierEmail =
                Value("SupplierEmail"),

            SupplierAddress =
                Value("SupplierAddress"),

            SupplierCity =
                Value("SupplierCity"),

            SupplierState =
                Value("SupplierState"),

            SupplierTaxNumber =
                Value("SupplierTaxNumber"),

            SupplierNotes =
                Value("SupplierNotes"),

            BatchNumber =
                Value("BatchNumber"),

            ManufactureDate =
                ParseOptionalDate(
                    Value("ManufactureDate"),
                    "ManufactureDate"),

            ExpiryDate =
                ParseOptionalDate(
                    Value("ExpiryDate"),
                    "ExpiryDate"),

            Quantity =
                ParseOptionalInt(
                    Value("Quantity"),
                    "Quantity"),

            PurchasePrice =
                ParseOptionalDecimal(
                    Value("PurchasePrice"),
                    "PurchasePrice"),

            SellingPrice =
                ParseOptionalDecimal(
                    Value("SellingPrice"),
                    "SellingPrice"),

            GST =
                ParseOptionalDecimal(
                    Value("GST"),
                    "GST"),

            MinimumStock =
                ParseOptionalInt(
                    Value("MinimumStock"),
                    "MinimumStock"),

            MaximumStock =
                ParseOptionalInt(
                    Value("MaximumStock"),
                    "MaximumStock"),

            ReorderLevel =
                ParseOptionalInt(
                    Value("ReorderLevel"),
                    "ReorderLevel"),

            StorageLocation =
                Value("StorageLocation")
        };
    }

    // ============================================================
    // ROW VALIDATION
    // ============================================================

    private static void ValidateRow(
        MedicineImportDataDto data)
    {
        var errors =
            new List<string>();

        static void AddRequiredError(
            List<string> target,
            string? value,
            string columnName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                target.Add(
                    $"{columnName} is required.");
            }
        }

        AddRequiredError(errors, data.Name, "Name");

        if (data.Quantity < 0)
        {
            errors.Add(
                "Quantity cannot be negative.");
        }

        if (data.Quantity > 0 && (string.IsNullOrWhiteSpace(data.BatchNumber) ||
            data.ManufactureDate == default || data.ExpiryDate == default))
        {
            errors.Add("Positive StockQuantity requires BatchNumber, ManufactureDate and ExpiryDate.");
        }

        if (data.ManufactureDate != default &&
            data.ExpiryDate != default &&
            data.ManufactureDate >= data.ExpiryDate)
        {
            errors.Add(
                "ManufactureDate must be before ExpiryDate.");
        }

        if (data.ExpiryDate != default &&
            data.ExpiryDate.Date <= DateTime.UtcNow.Date)
        {
            errors.Add(
                "ExpiryDate must be in the future.");
        }

        if (data.PurchasePrice < 0)
        {
            errors.Add(
                "PurchasePrice cannot be negative.");
        }

        if (data.SellingPrice < 0)
        {
            errors.Add(
                "SellingPrice cannot be negative.");
        }

        if (data.GST < 0 ||
            data.GST > 100)
        {
            errors.Add(
                "GST must be between 0 and 100.");
        }

        if (data.MinimumStock < 0)
        {
            errors.Add(
                "MinimumStock cannot be negative.");
        }

        if (data.MaximumStock < 0)
        {
            errors.Add(
                "MaximumStock cannot be negative.");
        }

        if (data.ReorderLevel < 0)
        {
            errors.Add(
                "ReorderLevel cannot be negative.");
        }

        if (data.MaximumStock > 0 &&
            data.MaximumStock < data.MinimumStock)
        {
            errors.Add(
                "MaximumStock cannot be less than MinimumStock.");
        }

        if (data.MaximumStock > 0 &&
            data.ReorderLevel > data.MaximumStock)
        {
            errors.Add(
                "ReorderLevel cannot be greater than MaximumStock.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                string.Join(" ", errors));
        }
    }

    // ============================================================
    // DATABASE VALIDATION
    // ============================================================

    private async Task ValidateReferencesAsync(
        MedicineImportDataDto data,
        int pharmacyId,
        int branchId)
    {
        /*
         * This is where validations against EXISTING
         * database tables can be placed.
         *
         * We do not insert anything here.
         */

        if (string.IsNullOrWhiteSpace(
                data.MedicineCode))
        {
            return;
        }

        /*
         * Example:
         *
         * If your business requires Category to already
         * exist in a Category table, validate it here.
         *
         * Do not add fake/sample categories.
         */

        /*
        var categoryExists =
            await _db.MedicineCategories
                .AsNoTracking()
                .AnyAsync(x =>
                    x.PharmacyId == pharmacyId &&
                    x.BranchId == branchId &&
                    x.Name == data.Category);

        if (!categoryExists)
        {
            throw new Exception(
                $"Category '{data.Category}' does not exist.");
        }
        */

        await Task.CompletedTask;
    }

    // ============================================================
    // SUPPLIER
    // ============================================================

    private async Task<Supplier?>
        UpsertSupplierAsync(
            MedicineImportDataDto data,
            int pharmacyId,
            int branchId,
            MedicineImport import,
            bool importSupplier)
    {
        var code = (data.SupplierCode ?? string.Empty).Trim();
        var name = (data.SupplierName ?? string.Empty).Trim();

        // No supplier information in this row.
        if (string.IsNullOrWhiteSpace(code) &&
            string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Supplier? supplier = null;

        // Prefer supplier code because it is the stable business identifier.
        if (!string.IsNullOrWhiteSpace(code))
        {
            supplier = await _db.Suppliers
                .FirstOrDefaultAsync(x =>
                    x.PharmacyId == pharmacyId &&
                    x.BranchId == branchId &&
                    x.Code == code);
        }

        // Fallback to name so files without SupplierCode can still reuse
        // an existing supplier instead of creating duplicates.
        if (supplier == null &&
            !string.IsNullOrWhiteSpace(name))
        {
            supplier = await _db.Suppliers
                .FirstOrDefaultAsync(x =>
                    x.PharmacyId == pharmacyId &&
                    x.BranchId == branchId &&
                    x.Name == name);
        }

        // ImportSuppliers=false means: only link an already existing supplier.
        // Do not create or update supplier master data.
        if (!importSupplier)
        {
            return supplier;
        }

        if (supplier == null)
        {
            supplier = new Supplier
            {
                PharmacyId = pharmacyId,
                BranchId = branchId,
                Code = code,
                Name = !string.IsNullOrWhiteSpace(name)
                    ? name
                    : code,
                ContactPerson = data.SupplierContactPerson?.Trim() ?? string.Empty,
                Phone = data.SupplierPhone?.Trim() ?? string.Empty,
                Email = data.SupplierEmail?.Trim() ?? string.Empty,
                Address = data.SupplierAddress?.Trim() ?? string.Empty,
                City = data.SupplierCity?.Trim() ?? string.Empty,
                State = data.SupplierState?.Trim() ?? string.Empty,
                TaxNumber = data.SupplierTaxNumber?.Trim() ?? string.Empty,
                Notes = data.SupplierNotes?.Trim() ?? string.Empty,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Suppliers.Add(supplier);
            import.CreatedSuppliers++;

            return supplier;
        }

        // Update only values actually supplied by the uploaded file.
        // Blank optional columns must not erase existing supplier details.
        if (!string.IsNullOrWhiteSpace(code))
            supplier.Code = code;

        if (!string.IsNullOrWhiteSpace(name))
            supplier.Name = name;

        if (!string.IsNullOrWhiteSpace(data.SupplierContactPerson))
            supplier.ContactPerson = data.SupplierContactPerson.Trim();

        if (!string.IsNullOrWhiteSpace(data.SupplierPhone))
            supplier.Phone = data.SupplierPhone.Trim();

        if (!string.IsNullOrWhiteSpace(data.SupplierEmail))
            supplier.Email = data.SupplierEmail.Trim();

        if (!string.IsNullOrWhiteSpace(data.SupplierAddress))
            supplier.Address = data.SupplierAddress.Trim();

        if (!string.IsNullOrWhiteSpace(data.SupplierCity))
            supplier.City = data.SupplierCity.Trim();

        if (!string.IsNullOrWhiteSpace(data.SupplierState))
            supplier.State = data.SupplierState.Trim();

        if (!string.IsNullOrWhiteSpace(data.SupplierTaxNumber))
            supplier.TaxNumber = data.SupplierTaxNumber.Trim();

        if (!string.IsNullOrWhiteSpace(data.SupplierNotes))
            supplier.Notes = data.SupplierNotes.Trim();

        supplier.IsActive = true;

        return supplier;
    }

    // ============================================================
    // MEDICINE
    // ============================================================

    private async Task<Medicine>
        UpsertMedicineAsync(
            MedicineImportDataDto data,
            int pharmacyId,
            int branchId,
            MedicineImport import,
            bool updateExisting)
    {
        var medicine =
            await _db.Medicines
                .FirstOrDefaultAsync(x =>
                    x.PharmacyId == pharmacyId &&
                    x.BranchId == branchId &&
                    x.MedicineCode == data.MedicineCode);

        if (medicine == null)
        {
            medicine = new Medicine
            {
                PharmacyId = pharmacyId,
                BranchId = branchId,

                MedicineCode =
                    data.MedicineCode,

                IsActive = true
            };

            _db.Medicines.Add(medicine);

            import.CreatedMedicines++;
        }
        else
        {
            if (!updateExisting)
            {
                throw new Exception(
                    $"Medicine '{data.MedicineCode}' already exists.");
            }

            import.UpdatedMedicines++;
        }

        /*
         * These values come from the uploaded row.
         *
         * Nothing here is hard-coded.
         */
        medicine.Name =
            data.Name;

        medicine.GenericName =
            data.GenericName;

        medicine.BrandName =
            data.BrandName;

        medicine.Category =
            data.Category;

        medicine.DosageForm =
            data.DosageForm;

        medicine.Strength =
            data.Strength;

        medicine.Manufacturer =
            data.Manufacturer;

        medicine.PackSize =
            data.PackSize;

        medicine.PurchasePrice =
            data.PurchasePrice;

        medicine.SellingPrice =
            data.SellingPrice;

        medicine.GST =
            data.GST;

        if (data.ExpiryDate != default)
        {
            medicine.ExpiryDate =
                data.ExpiryDate;
        }

        medicine.IsActive =
            true;

        return medicine;
    }

    // ============================================================
    // STOCK / INVENTORY / BATCH
    // ============================================================

    private async Task ImportStockAsync(
        MedicineImportDataDto data,
        Medicine medicine,
        Supplier? supplier,
        MedicineImport import,
        MedicineImportRow row,
        int pharmacyId,
        int branchId,
        int userId,
        string duplicateBatchAction)
    {
        var inventory =
            await _db.Inventories
                .FirstOrDefaultAsync(x =>
                    x.PharmacyId == pharmacyId &&
                    x.BranchId == branchId &&
                    x.MedicineId == medicine.Id);

        if (inventory == null)
        {
            inventory = new Inventory
            {
                PharmacyId = pharmacyId,
                BranchId = branchId,

                MedicineId = medicine.Id,

                CurrentStock = 0,
                AvailableStock = 0
            };

            _db.Inventories.Add(inventory);

            /*
             * Need inventory ID before creating batch.
             */
            await _db.SaveChangesAsync();
        }

        var batch =
            await _db.MedicineBatches
                .FirstOrDefaultAsync(x =>
                    x.InventoryId == inventory.Id &&
                    x.BatchNumber == data.BatchNumber);

        var quantityToAdd =
            data.Quantity;

        if (batch != null)
        {
            if (duplicateBatchAction.Equals(
                    "Skip",
                    StringComparison.OrdinalIgnoreCase))
            {
                /*
                 * Skip means NO stock should be added.
                 */
                return;
            }

            if (!duplicateBatchAction.Equals(
                    "AddQuantity",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"Batch '{data.BatchNumber}' already exists " +
                    $"for medicine '{data.MedicineCode}'.");
            }

            batch.QuantityReceived +=
                quantityToAdd;

            batch.RemainingQuantity +=
                quantityToAdd;

            /*
             * Refresh batch information if desired.
             */
            batch.ManufactureDate =
                data.ManufactureDate;

            batch.ExpiryDate =
                data.ExpiryDate;

            batch.PurchasePrice =
                data.PurchasePrice;

            batch.SellingPrice =
                data.SellingPrice;

            batch.GST =
                data.GST;

            if (supplier != null)
            {
                batch.SupplierId =
                    supplier.Id;
            }
        }
        else
        {
            batch = new MedicineBatch
            {
                InventoryId =
                    inventory.Id,

                SupplierId =
                    supplier?.Id,

                BatchNumber =
                    data.BatchNumber,

                ManufactureDate =
                    data.ManufactureDate,

                ExpiryDate =
                    data.ExpiryDate,

                PurchasePrice =
                    data.PurchasePrice,

                SellingPrice =
                    data.SellingPrice,

                GST =
                    data.GST,

                QuantityReceived =
                    quantityToAdd,

                RemainingQuantity =
                    quantityToAdd
            };

            _db.MedicineBatches.Add(batch);

            import.CreatedBatches++;
        }

        inventory.CurrentStock +=
            quantityToAdd;

        inventory.AvailableStock +=
            quantityToAdd;

        // Medicine is branch-owned in this standalone PMS. Keep its legacy
        // StockQuantity column consistent with the branch inventory after
        // every successful direct import (including additional batches).
        // AvailableStock, rather than received units, is the displayed stock.
        medicine.StockQuantity = inventory.AvailableStock;

        inventory.MinimumStock =
            data.MinimumStock;

        inventory.MaximumStock =
            data.MaximumStock;

        inventory.ReorderLevel =
            data.ReorderLevel;

        inventory.StorageLocation =
            data.StorageLocation;

        inventory.LastUpdated =
            DateTime.UtcNow;

        /*
         * Save so batch.Id exists for StockTransaction.
         */
        await _db.SaveChangesAsync();

        var transaction =
            new StockTransaction
            {
                PharmacyId =
                    pharmacyId,

                BranchId =
                    branchId,

                MedicineId =
                    medicine.Id,

                MedicineBatchId =
                    batch.Id,

                TransactionType =
                    "BulkImport",

                Quantity =
                    quantityToAdd,

                BalanceStock =
                    inventory.CurrentStock,

                Remarks =
                    $"Medicine bulk import #{import.Id}",

                ReferenceType =
                    "MedicineImport",

                ReferenceId =
                    import.Id,

                CreatedByUserId =
                    userId,

                TransactionDate =
                    DateTime.UtcNow
            };

        _db.StockTransactions.Add(
            transaction);

        import.StockUnitsAdded +=
            quantityToAdd;

        row.InventoryId =
            inventory.Id;

        row.MedicineBatchId =
            batch.Id;
    }

    // ============================================================
    // COMMIT OPTION VALIDATION
    // ============================================================

    private static void ValidateCommitOptions(
        CommitMedicineImportDto dto)
    {
        var allowedActions =
            new[]
            {
                "Error",
                "Skip",
                "AddQuantity"
            };

        if (string.IsNullOrWhiteSpace(
                dto.DuplicateBatchAction))
        {
            dto.DuplicateBatchAction =
                "Error";
        }

        if (!allowedActions.Contains(
                dto.DuplicateBatchAction,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new Exception(
                "DuplicateBatchAction must be " +
                "'Error', 'Skip', or 'AddQuantity'.");
        }
    }

    // ============================================================
    // PARSERS
    // ============================================================

    private static DateTime ParseOptionalDate(
        string value,
        string column)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return default;
        }

        var supportedFormats =
            new[]
            {
                "yyyy-MM-dd",
                "dd-MM-yyyy",
                "dd/MM/yyyy",
                "MM/dd/yyyy",
                "yyyy/MM/dd"
            };

        if (DateTime.TryParseExact(
                value,
                supportedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exactResult))
        {
            return exactResult.Date;
        }

        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var result))
        {
            return result.Date;
        }

        throw new Exception(
            $"{column} has invalid date value '{value}'.");
    }


    private static int ParseOptionalInt(
        string value,
        string column)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        if (!int.TryParse(
                value.Trim(),
                NumberStyles.Integer |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var result))
        {
            throw new Exception(
                $"{column} has invalid integer value '{value}'.");
        }

        return result;
    }


    private static decimal ParseOptionalDecimal(
        string value,
        string column)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (!decimal.TryParse(
                value,
                NumberStyles.Number |
                NumberStyles.AllowCurrencySymbol,
                CultureInfo.InvariantCulture,
                out var result))
        {
            throw new Exception(
                $"{column} has invalid decimal value '{value}'.");
        }

        return result;
    }

    // ============================================================
    // EXCEL VALUE HANDLING
    // ============================================================

    private static string GetExcelCellValue(
        IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        /*
         * Dates require special handling because Excel
         * internally stores them as numeric values.
         */
        if (cell.DataType == XLDataType.DateTime)
        {
            return cell
                .GetDateTime()
                .ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);
        }

        return cell
            .GetFormattedString()
            .Trim();
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static string GetDictionaryValue(
        Dictionary<string, string> values,
        string column)
    {
        if (!values.TryGetValue(
                column,
                out var value))
        {
            return string.Empty;
        }

        return value?.Trim()
               ?? string.Empty;
    }

    private static string EscapeCsv(
        string value)
    {
        return
            $"\"{value.Replace("\"", "\"\"")}\"";
    }

    // ============================================================
    // INTERNAL FILE ROW MODEL
    // ============================================================

    private sealed class UploadedMedicineRow
    {
        public int RowNumber { get; set; }

        public Dictionary<string, string>
            Values
        { get; set; } =
                new(
                    StringComparer.OrdinalIgnoreCase);
    }
}

