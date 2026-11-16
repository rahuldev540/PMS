using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/medicine")]
[Authorize(Roles = "PharmacyAdmin,Pharmacist")]
public class MedicineController : PharmacyApiControllerBase
{
    private readonly IMedicineService _medicineService;
    private readonly AppDbContext _db;
    private readonly PharmacyPermissionChecker _permissionChecker;

    public MedicineController(
        IMedicineService medicineService,
        AppDbContext db,
        PharmacyPermissionChecker permissionChecker)
    {
        _medicineService = medicineService;
        _db = db;
        _permissionChecker = permissionChecker;
    }

    // ============================================================
    // CHECK MEDICINE PERMISSION
    //
    // PharmacyAdmin and Pharmacist both use UserPermissions.
    // PharmacyPermissionRole / PharmacyRolePermissions are removed.
    // ============================================================

    private async Task<IActionResult?> CheckMedicinePermission(string action)
    {
        const string module = "Medicines";

        if (string.IsNullOrWhiteSpace(action))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    Success = false,
                    Message = "Invalid permission request."
                });
        }

        var allowed = await _permissionChecker.HasPermissionAsync(
            UserId,
            module,
            action.Trim());

        if (allowed)
            return null;

        return StatusCode(
            StatusCodes.Status403Forbidden,
            new
            {
                Success = false,
                Message = $"You do not have {action.Trim()} permission for Medicines."
            });
    }

    // Resolve the medicine's actual internal branch ID. Admins can manage
    // any branch within their pharmacy; pharmacists only their own branch.
    private async Task<(int? BranchId, IActionResult? Error)> ResolveMedicineBranchAsync(int id)
    {
        var medicine = await _db.Medicines.AsNoTracking()
            .Where(m => m.Id == id && m.PharmacyId == PharmacyId && m.IsActive)
            .Select(m => new { m.BranchId })
            .FirstOrDefaultAsync();

        if (medicine == null)
            return (null, NotFound(new { Success = false, Message = "Medicine not found or already deleted." }));

        var validBranch = await _db.Branches.AsNoTracking()
            .AnyAsync(b => b.Id == medicine.BranchId && b.PharmacyId == PharmacyId && b.IsActive);
        if (!validBranch)
            return (null, NotFound(new { Success = false, Message = "Medicine branch not found." }));

        if (User.IsInRole("Pharmacist") && medicine.BranchId != BranchId)
            return (null, StatusCode(403, new { Success = false, Message = "You cannot modify medicines from another branch." }));

        return (medicine.BranchId, null);
    }

    // ============================================================
    // CREATE MEDICINE
    //
    // POST /api/medicine
    //
    // PharmacyAdmin
    // Medicines -> Create
    // ============================================================

    [HttpPost]
    [Authorize(Roles = "PharmacyAdmin,Pharmacist")]
    public async Task<IActionResult> CreateMedicine(
        [FromBody] CreateMedicineDto dto)
    {
        var permissionError =
            await CheckMedicinePermission(
                "Create");

        if (permissionError != null)
            return permissionError;

        var result =
            await _medicineService
                .CreateMedicineAsync(
                    dto,
                    PharmacyId,
                    BranchId);

        return Ok(new
        {
            Success = true,
            Message = result
        });
    }

    // ============================================================
    // GET ALL MEDICINES
    //
    // GET /api/medicine
    //
    // PharmacyAdmin
    // Medicines -> View
    // ============================================================

    [HttpGet]
    [Authorize(Roles = "PharmacyAdmin,Pharmacist")]
    public async Task<IActionResult> GetAllMedicines([FromQuery] int? branchId = null)
    {
        var permissionError =
            await CheckMedicinePermission(
                "View");

        if (permissionError != null)
            return permissionError;

        // Public branch IDs restart at 1 within each pharmacy. Transactional
        // medicine/stock records use the internal Branch.Id.
        var branches = await _db.PharmacyBranchIdentities.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId)
            .Select(x => new { x.BranchId, x.ExistingBranchId })
            .ToListAsync();

        if (User.IsInRole("Pharmacist"))
        {
            var assignedInternalId = BranchId;
            var assigned = branches.FirstOrDefault(x => x.ExistingBranchId == assignedInternalId);
            if (assigned == null)
                return StatusCode(403, new { Success = false, Message = "No valid assigned pharmacy branch." });
            if (branchId.HasValue && branchId.Value != assigned.BranchId)
                return StatusCode(403, new { Success = false, Message = "You can view medicines only in your assigned branch." });
            return Ok(await _medicineService.GetAllMedicinesAsync(PharmacyId, assignedInternalId));
        }

        if (branchId.HasValue)
        {
            var selected = branches.FirstOrDefault(x => x.BranchId == branchId.Value);
            if (selected == null || !await _db.Branches.AnyAsync(x => x.Id == selected.ExistingBranchId && x.PharmacyId == PharmacyId && x.IsActive))
                return BadRequest(new { Success = false, Message = "Invalid pharmacy branch." });
            return Ok(await _medicineService.GetAllMedicinesAsync(PharmacyId, selected.ExistingBranchId));
        }

        // Admin without branch selection: all branches, clearly separated.
        var result = new List<object>();
        foreach (var branch in branches.OrderBy(x => x.BranchId))
        {
            if (!await _db.Branches.AnyAsync(x => x.Id == branch.ExistingBranchId && x.PharmacyId == PharmacyId && x.IsActive))
                continue;
            var medicines = await _medicineService.GetAllMedicinesAsync(PharmacyId, branch.ExistingBranchId);
            result.Add(new { branchId = branch.BranchId, totalMedicines = medicines.Count, medicines });
        }
        return Ok(new { success = true, pharmacyId = PharmacyId, branches = result });
    }

    // Opt-in paged listing. Existing GET /api/medicine remains unchanged.
    [HttpGet("paged")]
    [Authorize(Roles = "PharmacyAdmin,Pharmacist")]
    public async Task<IActionResult> GetMedicinesPaged(
        [FromQuery] int branchId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        var permissionError = await CheckMedicinePermission("View");
        if (permissionError != null) return permissionError;
        if (pageNumber < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "pageNumber must be >= 1 and pageSize must be 1..100." });
        var mapping = await _db.PharmacyBranchIdentities.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.BranchId == branchId)
            .Select(x => new { x.ExistingBranchId }).FirstOrDefaultAsync();
        if (mapping == null) return BadRequest(new { message = "Invalid pharmacy branch." });
        if (User.IsInRole("Pharmacist") && mapping.ExistingBranchId != BranchId)
            return StatusCode(403, new { message = "You can view only your assigned branch." });
        if (!await _db.Branches.AnyAsync(x => x.Id == mapping.ExistingBranchId && x.PharmacyId == PharmacyId && x.IsActive))
            return BadRequest(new { message = "Inactive or invalid branch." });
        var totalCount = await _db.Medicines.AsNoTracking().CountAsync(x =>
            x.PharmacyId == PharmacyId && x.BranchId == mapping.ExistingBranchId && x.IsActive);
        var items = await _medicineService.GetMedicinesPageAsync(PharmacyId, mapping.ExistingBranchId, pageNumber, pageSize);
        return Ok(new { pharmacyId = PharmacyId, branchId, pageNumber, pageSize, totalCount, items });
    }

    // ============================================================
    // GET AVAILABLE MEDICINES
    //
    // GET /api/medicine/available
    //
    // Returns medicines considered available by IMedicineService
    // for the current PharmacyId + BranchId.
    //
    // A medicine with AvailableStock = 0 normally will not appear here.
    // GET /api/medicine returns the complete medicine master list.
    //
    // PharmacyAdmin / Pharmacist
    // Medicines -> View
    // ============================================================

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableMedicines()
    {
        var permissionError =
            await CheckMedicinePermission(
                "View");

        if (permissionError != null)
            return permissionError;

        if (User.IsInRole("PharmacyAdmin"))
        {
            var internalBranchIds = await _db.Branches.AsNoTracking()
                .Where(b => b.PharmacyId == PharmacyId && b.IsActive)
                .Select(b => b.Id)
                .ToListAsync();
            var all = new List<MedicineSearchDto>();
            foreach (var internalId in internalBranchIds)
                all.AddRange(await _medicineService.GetAvailableMedicinesAsync(
                    PharmacyId, internalId));
            return Ok(all);
        }

        var medicines = await _medicineService.GetAvailableMedicinesAsync(
            PharmacyId, BranchId);
        return Ok(medicines);
    }

    // ============================================================
    // GET MEDICINE CATEGORIES
    //
    // GET /api/medicine/categories
    //
    // Medicines -> View
    // ============================================================

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var permissionError =
            await CheckMedicinePermission(
                "View");

        if (permissionError != null)
            return permissionError;

        var items = new List<MedicineDto>();
        if (User.IsInRole("PharmacyAdmin"))
        {
            var internalBranchIds = await _db.Branches.AsNoTracking()
                .Where(b => b.PharmacyId == PharmacyId && b.IsActive)
                .Select(b => b.Id)
                .ToListAsync();
            foreach (var internalId in internalBranchIds)
                items.AddRange(await _medicineService.GetAllMedicinesAsync(
                    PharmacyId, internalId));
        }
        else
        {
            items = await _medicineService.GetAllMedicinesAsync(PharmacyId, BranchId);
        }

        var categories =
            items
                .Select(x => x.Category)
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

        return Ok(categories);
    }

    // ============================================================
    // GET DOSAGE FORMS
    //
    // GET /api/medicine/dosage-forms
    //
    // Medicines -> View
    // ============================================================

    [HttpGet("dosage-forms")]
    public async Task<IActionResult> GetDosageForms()
    {
        var permissionError =
            await CheckMedicinePermission(
                "View");

        if (permissionError != null)
            return permissionError;

        var items = new List<MedicineDto>();
        if (User.IsInRole("PharmacyAdmin"))
        {
            var internalBranchIds = await _db.Branches.AsNoTracking()
                .Where(b => b.PharmacyId == PharmacyId && b.IsActive)
                .Select(b => b.Id)
                .ToListAsync();
            foreach (var internalId in internalBranchIds)
                items.AddRange(await _medicineService.GetAllMedicinesAsync(
                    PharmacyId, internalId));
        }
        else
        {
            items = await _medicineService.GetAllMedicinesAsync(PharmacyId, BranchId);
        }

        var dosageForms =
            items
                .Select(x => x.DosageForm)
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

        return Ok(dosageForms);
    }

    // ============================================================
    // GET STRENGTHS
    //
    // GET /api/medicine/strengths
    //
    // Medicines -> View
    // ============================================================

    [HttpGet("strengths")]
    public async Task<IActionResult> GetStrengths()
    {
        var permissionError =
            await CheckMedicinePermission(
                "View");

        if (permissionError != null)
            return permissionError;

        var items = new List<MedicineDto>();
        if (User.IsInRole("PharmacyAdmin"))
        {
            var internalBranchIds = await _db.Branches.AsNoTracking()
                .Where(b => b.PharmacyId == PharmacyId && b.IsActive)
                .Select(b => b.Id)
                .ToListAsync();
            foreach (var internalId in internalBranchIds)
                items.AddRange(await _medicineService.GetAllMedicinesAsync(
                    PharmacyId, internalId));
        }
        else
        {
            items = await _medicineService.GetAllMedicinesAsync(PharmacyId, BranchId);
        }

        var strengths =
            items
                .Select(x => x.Strength)
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

        return Ok(strengths);
    }

    // ============================================================
    // GET MEDICINE BY ID
    //
    // GET /api/medicine/{id}
    //
    // Medicines -> View
    // ============================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetMedicine(
        int id)
    {
        var permissionError =
            await CheckMedicinePermission(
                "View");

        if (permissionError != null)
            return permissionError;

        if (id <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid medicine id is required."
            });
        }

        // Pharmacy admins can read medicines in any branch of THEIR pharmacy.
        // Pharmacists remain restricted to their assigned internal branch.
        var effectiveBranchId = BranchId;
        if (User.IsInRole("PharmacyAdmin"))
        {
            var target = await _db.Medicines.AsNoTracking()
                .Where(m => m.Id == id && m.PharmacyId == PharmacyId && m.IsActive)
                .Select(m => new { m.BranchId })
                .FirstOrDefaultAsync();

            if (target == null)
                return NotFound(new { Success = false, Message = "Medicine not found." });

            var authorizedBranch = await _db.Branches.AsNoTracking()
                .AnyAsync(b => b.Id == target.BranchId &&
                               b.PharmacyId == PharmacyId && b.IsActive);
            if (!authorizedBranch)
                return NotFound(new { Success = false, Message = "Medicine not found." });

            effectiveBranchId = target.BranchId;
        }

        var medicine = await _medicineService.GetMedicineByIdAsync(
            id, PharmacyId, effectiveBranchId);

        if (medicine == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Medicine not found."
            });
        }

        return Ok(medicine);
    }

    // ============================================================
    // UPDATE MEDICINE
    //
    // PUT /api/medicine/{id}
    //
    // PharmacyAdmin
    // Medicines -> Edit
    // ============================================================

    [HttpPut("{id:int}")]
    [Authorize(Roles = "PharmacyAdmin,Pharmacist")]
    public async Task<IActionResult> UpdateMedicine(int id, [FromBody] UpdateMedicineDto dto)
    {
        var permissionError = await CheckMedicinePermission("Edit");
        if (permissionError != null) return permissionError;
        if (id <= 0)
            return BadRequest(new { Success = false, Message = "A valid medicine id is required." });

        var (targetBranchId, error) = await ResolveMedicineBranchAsync(id);
        if (error != null) return error;

        try
        {
            var result = await _medicineService.UpdateMedicineAsync(
                id, dto, PharmacyId, targetBranchId!.Value);
            return Ok(new { Success = true, Message = result });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { Success = false, Message = "Medicine not found." });
        }
    }

    // DELETE /api/medicine/{id} - permanent delete only if no dependent records exist.
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> DeleteMedicine(int id)
    {
        var permissionError = await CheckMedicinePermission("Delete");
        if (permissionError != null) return permissionError;
        if (id <= 0)
            return BadRequest(new { Success = false, Message = "A valid medicine id is required." });

        var (targetBranchId, error) = await ResolveMedicineBranchAsync(id);
        if (error != null) return error;

        try
        {
            var result = await _medicineService.DeleteMedicineAsync(
                id, PharmacyId, targetBranchId!.Value);
            return Ok(new { Success = true, Message = result });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { Success = false, Message = "Medicine not found or already deleted." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Success = false, Message = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { Success = false, Message = "Medicine is referenced by another record; permanent deletion blocked." });
        }
    }

    // ============================================================
    // SEARCH MEDICINE
    //
    // GET /api/medicine/search?keyword=...
    //
    // Medicines -> View
    // ============================================================

    [HttpGet("search")]
    public async Task<IActionResult> SearchMedicine(
        [FromQuery] string keyword)
    {
        var permissionError =
            await CheckMedicinePermission(
                "View");

        if (permissionError != null)
            return permissionError;

        if (string.IsNullOrWhiteSpace(
                keyword))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Keyword is required."
            });
        }

        if (User.IsInRole("PharmacyAdmin"))
        {
            var internalBranchIds = await _db.Branches.AsNoTracking()
                .Where(b => b.PharmacyId == PharmacyId && b.IsActive)
                .Select(b => b.Id)
                .ToListAsync();

            var results = new List<MedicineSearchDto>();
            foreach (var internalId in internalBranchIds)
            {
                results.AddRange(await _medicineService.SearchMedicineAsync(
                    keyword.Trim(), PharmacyId, internalId));
            }
            return Ok(results.OrderBy(m => m.Name).Take(20).ToList());
        }

        var medicines = await _medicineService.SearchMedicineAsync(
            keyword.Trim(), PharmacyId, BranchId);
        return Ok(medicines);
    }
}

