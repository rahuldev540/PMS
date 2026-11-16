
using System.Security.Claims;
using AuthDemo.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Middleware;

public sealed class PharmacyAssignmentMiddleware
{
    private readonly RequestDelegate _next;

    public PharmacyAssignmentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var role = context.User.FindFirstValue(ClaimTypes.Role);

        var requiresAssignment =
            role is "PharmacyAdmin" or "Pharmacist";

        if (!requiresAssignment)
        {
            await _next(context);
            return;
        }

        var pharmacyClaim =
            context.User.FindFirstValue("PharmacyId");

        if (string.IsNullOrWhiteSpace(pharmacyClaim))
        {
            pharmacyClaim =
                context.User.FindFirstValue("pharmacyId");
        }

        var pharmacyRequestPath = context.Request.Path;
        var method = context.Request.Method;

        // Only Pharmacy Admin can access the status endpoint
        // when the pharmacy is inactive.
        var isRecoveryRoute =
            role == "PharmacyAdmin" &&
            HttpMethods.IsPatch(method) &&
            pharmacyRequestPath.Equals(
                new PathString(
                    "/api/pharmacy/settings/pharmacy-status"));

        // Admin account recovery endpoints remain accessible when a pharmacy
        // is inactive. Pharmacist and operational endpoints remain restricted.
        var isAdminAccountRoute =
            role == "PharmacyAdmin" &&
            (
                pharmacyRequestPath.Equals(new PathString("/api/pharmacy-admin-auth/assignment-status")) && HttpMethods.IsGet(method)
                || pharmacyRequestPath.Equals(new PathString("/api/pharmacy-admin-auth/change-password")) && HttpMethods.IsPost(method)
                || pharmacyRequestPath.Equals(new PathString("/api/pharmacy-admin-auth/logout")) && HttpMethods.IsPost(method)
            );

        // Check pharmacy active status using existing IsActive column.
        if (int.TryParse(pharmacyClaim, out var pharmacyId)
            && pharmacyId > 0)
        {
            if (!isRecoveryRoute && !isAdminAccountRoute)
            {
                var db = context.RequestServices
                    .GetRequiredService<AppDbContext>();

                var state = await db.Pharmacies
                    .AsNoTracking()
                    .Where(p => p.Id == pharmacyId)
                    .Select(p => new
                    {
                        p.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (state == null || !state.IsActive)
                {
                    context.Response.StatusCode =
                        StatusCodes.Status403Forbidden;

                    await context.Response.WriteAsJsonAsync(new
                    {
                        Success = false,
                        Message = "Pharmacy is inactive or unavailable."
                    });

                    return;
                }
            }
        }

        var assigned = string.Equals(
            context.User.FindFirstValue("IsPharmacyAssigned"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (assigned)
        {
            await _next(context);
            return;
        }

        var isPharmacyRegistration =
            role == "PharmacyAdmin" &&
            HttpMethods.IsPost(method) &&
            pharmacyRequestPath.Equals(
                new PathString(
                    "/api/pharmacy-admin/pharmacy"));

        var isBasicAccountEndpoint =
            role == "PharmacyAdmin" &&
            (
                pharmacyRequestPath.Equals(
                    new PathString(
                        "/api/pharmacy-admin-auth/assignment-status"))
                ||
                pharmacyRequestPath.Equals(
                    new PathString(
                        "/api/pharmacy-admin-auth/change-password"))
                ||
                pharmacyRequestPath.Equals(
                    new PathString(
                        "/api/pharmacy-admin-auth/logout"))
            );

        if (isPharmacyRegistration || isBasicAccountEndpoint)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode =
            StatusCodes.Status403Forbidden;

        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            Success = false,
            Message =
                "A pharmacy tenant must be assigned before this operation is available.",
            AccountStatus = "PendingPharmacyAssignment",
            IsPharmacyAssigned = false,
            NextAction = role == "PharmacyAdmin"
                ? "Create your pharmacy to automatically create its main branch and activate your account."
                : "Your Pharmacy Admin must assign you to a pharmacy branch."
        });
    }
}
