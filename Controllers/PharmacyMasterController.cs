using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy-super-admin/pharmacies")]
[Authorize(Roles = "PharmacySuperAdmin")]
public sealed class PharmacyMasterController : ControllerBase
{
    private readonly AppDbContext _db;
    public PharmacyMasterController(AppDbContext db) => _db = db;

    // Pharmacy registration is exclusively performed by the authenticated Pharmacy Admin.
    // POST /api/pharmacy-admin/pharmacy

    [HttpGet]
    public async Task<IActionResult> GetAll(string? search, string? city, string? district, string? state, string? postalCode, bool? isActive, int page=1, int pageSize=50)
    {
        page=Math.Max(1,page); pageSize=Math.Clamp(pageSize,1,100);
        var q=_db.Pharmacies.AsNoTracking().AsQueryable();
        if (isActive.HasValue) q=q.Where(x=>x.IsActive==isActive.Value);
        if (!string.IsNullOrWhiteSpace(city)) q=q.Where(x=>(x.City??"").Contains(city));
        if (!string.IsNullOrWhiteSpace(district)) q=q.Where(x=>(x.District??"").Contains(district));
        if (!string.IsNullOrWhiteSpace(state)) q=q.Where(x=>(x.State??"").Contains(state));
        if (!string.IsNullOrWhiteSpace(postalCode)) q=q.Where(x=>(x.PostalCode??"").Contains(postalCode));
        if (!string.IsNullOrWhiteSpace(search)) { var k=search.Trim(); q=q.Where(x=>x.Name.Contains(k)||(x.Address??"").Contains(k)||(x.City??"").Contains(k)||(x.District??"").Contains(k)||(x.State??"").Contains(k)||(x.PostalCode??"").Contains(k)||(x.Phone??"").Contains(k)||(x.Email??"").Contains(k)); }
        var total=await q.CountAsync();
        var data=await q.OrderBy(x=>x.Name).Skip((page-1)*pageSize).Take(pageSize).Select(x=>new { x.Id,x.Name,x.Phone,x.Email,x.Address,x.City,x.State,x.District,x.Country,x.PostalCode,x.IsActive,x.IsDeleted,x.DeletedAt,x.CreatedAt,x.UpdatedAt, TotalBranches=x.Branches.Count(b=>b.IsActive), MainBranch=x.Branches.Where(b=>b.IsMainBranch).Select(b=>new {b.Id,b.Name,b.City,b.District,b.State,b.PostalCode}).FirstOrDefault(), AssignedAdmins=_db.Users.Where(u=>u.Role=="PharmacyAdmin" && u.PharmacyId==x.Id).OrderBy(u=>u.Name).Select(u=>new {u.Id,u.Name,u.Email,u.MobileNumber,u.AccountStatus,u.IsActive,u.BranchId,BranchName=u.Branch!=null?u.Branch.Name:string.Empty}).ToList() }).ToListAsync();
        return Ok(new {Success=true,Total=total,Page=page,PageSize=pageSize,Pharmacies=data});
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var x=await _db.Pharmacies.AsNoTracking().Where(p=>p.Id==id).Select(p=>new { p.Id,p.Name,p.Phone,p.Email,p.Address,p.City,p.State,p.District,p.Country,p.PostalCode,p.IsActive,p.IsDeleted,p.DeletedAt,p.CreatedAt,p.UpdatedAt, Branches=p.Branches.OrderByDescending(b=>b.IsMainBranch).ThenBy(b=>b.Name).Select(b=>new {b.Id,b.Name,b.IsMainBranch,b.IsActive,b.Phone,b.Email,b.Address,b.City,b.District,b.State,b.Country,b.PostalCode}), AssignedAdmins=_db.Users.Where(u=>u.Role=="PharmacyAdmin" && u.PharmacyId==p.Id).OrderBy(u=>u.Name).Select(u=>new {u.Id,u.Name,u.Email,u.MobileNumber,u.AccountStatus,u.IsActive,u.BranchId,BranchName=u.Branch!=null?u.Branch.Name:string.Empty}).ToList() }).FirstOrDefaultAsync();
        return x==null ? NotFound(new {Success=false,Message="Pharmacy was not found."}) : Ok(new {Success=true,Pharmacy=x});
    }

    // Pharmacy activation/deactivation is owned by PharmacyAdmin only.

}
