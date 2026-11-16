using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize(Roles = "PharmacyAdmin")]
public class SupplierController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyPermissionChecker _permissionChecker;

    public SupplierController(
        AppDbContext db,
        PharmacyPermissionChecker permissionChecker)
    {
        _db = db;
        _permissionChecker = permissionChecker;
    }

    // =====================================================
    // CHECK SUPPLIERS PERMISSION
    // =====================================================

    private async Task<IActionResult?> CheckSupplierPermission(
        string action)
    {
        var allowed =
            await _permissionChecker.HasPermissionAsync(
                UserId,
                "Suppliers",
                action);

        if (!allowed)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    Success = false,
                    Message =
                        $"You do not have {action} permission for Suppliers."
                });
        }

        return null;
    }

    // =====================================================
    // CREATE SUPPLIER
    // POST /api/suppliers
    //
    // Suppliers -> Create
    // =====================================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SupplierUpsertDto dto)
    {
        var permissionError =
            await CheckSupplierPermission("Create");

        if (permissionError != null)
            return permissionError;

        var code = string.IsNullOrWhiteSpace(dto.Code)
            ? $"SUP-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            : dto.Code.Trim();
        dto.Code = code;

        var exists =
            await _db.Suppliers.AnyAsync(x =>
                x.PharmacyId == PharmacyId &&
                x.BranchId == BranchId &&
                x.Code == code);

        if (exists)
        {
            return Conflict(new
            {
                Success = false,
                Message =
                    "Supplier code already exists."
            });
        }

        var entity =
            Map(
                new Supplier
                {
                    PharmacyId = PharmacyId,
                    BranchId = BranchId
                },
                dto);

        _db.Suppliers.Add(entity);

        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(Get),
            new
            {
                id = entity.Id
            },
            new
            {
                Success = true,
                Message =
                    "Supplier created successfully.",
                Supplier = entity
            });
    }

    // =====================================================
    // LIST SUPPLIERS
    // GET /api/suppliers
    //
    // Suppliers -> View
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool activeOnly = true)
    {
        var permissionError =
            await CheckSupplierPermission("View");

        if (permissionError != null)
            return permissionError;

        var q =
            _db.Suppliers
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId);

        if (activeOnly)
        {
            q = q.Where(x =>
                x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword =
                search.Trim();

            q = q.Where(x =>
                x.Name.Contains(keyword) ||
                x.Code.Contains(keyword) ||
                x.ContactPerson.Contains(keyword) ||
                x.Phone.Contains(keyword) ||
                x.Email.Contains(keyword));
        }

        var items =
            await q
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Name,
                    x.ContactPerson,
                    x.Phone,
                    x.Email,
                    x.Address,
                    x.TaxNumber,
                    x.IsActive,
                    Status = x.IsActive ? "Active" : "Inactive",
                    MedicinesProducts = _db.PurchaseOrderItems
                        .Where(i => i.PurchaseOrder.SupplierId == x.Id)
                        .Select(i => i.Medicine.Name)
                        .Distinct()
                        .ToList(),
                    LastPurchase = _db.PurchaseOrders
                        .Where(p => p.SupplierId == x.Id && p.PharmacyId == PharmacyId && p.BranchId == BranchId)
                        .OrderByDescending(p => p.OrderDate)
                        .Select(p => (DateTime?)p.OrderDate)
                        .FirstOrDefault(),
                    TotalPurchases = _db.PurchaseOrderItems
                        .Where(i => i.PurchaseOrder.SupplierId == x.Id && i.PurchaseOrder.PharmacyId == PharmacyId && i.PurchaseOrder.BranchId == BranchId)
                        .Sum(i => (decimal?)(i.OrderedQuantity * i.UnitCost)) ?? 0m
                })
                .ToListAsync();

        var allSuppliers = _db.Suppliers.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId);

        return Ok(new
        {
            Success = true,
            Total = items.Count,
            Summary = new
            {
                TotalSuppliers = await allSuppliers.CountAsync(),
                ActiveSuppliers = await allSuppliers.CountAsync(x => x.IsActive),
                PendingSuppliers = await allSuppliers.CountAsync(x => !x.IsActive),
                TotalPurchases = await _db.PurchaseOrderItems
                    .Where(i => i.PurchaseOrder.PharmacyId == PharmacyId && i.PurchaseOrder.BranchId == BranchId)
                    .SumAsync(i => (decimal?)(i.OrderedQuantity * i.UnitCost)) ?? 0m
            },
            Items = items
        });
    }

    // =====================================================
    // GET SUPPLIER
    // GET /api/suppliers/{id}
    //
    // Suppliers -> View
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(
        int id)
    {
        var permissionError =
            await CheckSupplierPermission("View");

        if (permissionError != null)
            return permissionError;

        var item =
            await _db.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId);

        if (item == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Supplier not found."
            });
        }

        return Ok(new
        {
            Success = true,
            Supplier = item
        });
    }

    // =====================================================
    // UPDATE SUPPLIER
    // PUT /api/suppliers/{id}
    //
    // Suppliers -> Edit
    // =====================================================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] SupplierUpsertDto dto)
    {
        var permissionError =
            await CheckSupplierPermission("Edit");

        if (permissionError != null)
            return permissionError;

        var item =
            await _db.Suppliers
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId);

        if (item == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Supplier not found."
            });
        }

        var code = string.IsNullOrWhiteSpace(dto.Code)
            ? $"SUP-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            : dto.Code.Trim();
        dto.Code = code;

        var duplicateCode =
            await _db.Suppliers.AnyAsync(x =>
                x.Id != id &&
                x.PharmacyId == PharmacyId &&
                x.BranchId == BranchId &&
                x.Code == code);

        if (duplicateCode)
        {
            return Conflict(new
            {
                Success = false,
                Message =
                    "Supplier code already exists."
            });
        }

        Map(item, dto);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message =
                "Supplier updated successfully."
        });
    }

    // =====================================================
    // DELETE / DEACTIVATE SUPPLIER
    // DELETE /api/suppliers/{id}
    //
    // Suppliers -> Delete
    // =====================================================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id)
    {
        var permissionError =
            await CheckSupplierPermission("Delete");

        if (permissionError != null)
            return permissionError;

        var item =
            await _db.Suppliers
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId);

        if (item == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Supplier not found."
            });
        }

        item.IsActive = false;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message =
                "Supplier deactivated successfully."
        });
    }

    // =====================================================
    // CHANGE SUPPLIER STATUS
    // PATCH /api/suppliers/{id}/status
    //
    // Suppliers -> Edit
    // =====================================================

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> Status(
        int id,
        [FromBody] StatusDto dto)
    {
        var permissionError =
            await CheckSupplierPermission("Edit");

        if (permissionError != null)
            return permissionError;

        var item =
            await _db.Suppliers
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId);

        if (item == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Supplier not found."
            });
        }

        item.IsActive =
            dto.IsActive;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,

            Message =
                dto.IsActive
                    ? "Supplier activated successfully."
                    : "Supplier deactivated successfully."
        });
    }

    // =====================================================
    // PURCHASE HISTORY
    // GET /api/suppliers/{id}/purchase-history
    //
    // Suppliers -> View
    // =====================================================

    [HttpGet("{id:int}/purchase-history")]
    public async Task<IActionResult> PurchaseHistory(
        int id)
    {
        var permissionError =
            await CheckSupplierPermission("View");

        if (permissionError != null)
            return permissionError;

        var supplierExists =
            await _db.Suppliers
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == id &&
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId);

        if (!supplierExists)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Supplier not found."
            });
        }

        var items =
            await _db.PurchaseOrders
                .AsNoTracking()
                .Where(x =>
                    x.SupplierId == id &&
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId)
                .OrderByDescending(x =>
                    x.OrderDate)
                .Select(x => new
                {
                    x.Id,
                    x.OrderNumber,
                    x.OrderDate,
                    x.ExpectedDate,
                    x.Status,

                    Total =
                        x.Items.Sum(i =>
                            i.OrderedQuantity *
                            i.UnitCost)
                })
                .ToListAsync();

        return Ok(new
        {
            Success = true,
            Items = items
        });
    }

    // =====================================================
    // MAP DTO -> ENTITY
    // =====================================================

    private static Supplier Map(
        Supplier supplier,
        SupplierUpsertDto dto)
    {
        supplier.Code =
            dto.Code.Trim();

        supplier.Name =
            dto.SupplierName.Trim();

        supplier.ContactPerson =
            dto.ContactPerson?.Trim();

        supplier.Phone =
            dto.PhoneNumber?.Trim();

        supplier.Email =
            dto.EmailAddress?.Trim();

        supplier.Address =
            dto.Address?.Trim();

        supplier.TaxNumber =
            dto.TaxNumber?.Trim() ?? string.Empty;

        supplier.City = dto.City?.Trim() ?? string.Empty;
        supplier.State = dto.State?.Trim() ?? string.Empty;
        supplier.Notes = dto.Notes?.Trim() ?? string.Empty;
        supplier.IsActive = !string.Equals(dto.Status?.Trim(), "Inactive", StringComparison.OrdinalIgnoreCase);

        return supplier;
    }
}