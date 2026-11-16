using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AuthDemo.Swagger;

/// <summary>Improves Swagger labels only; it does not change routes or behavior.</summary>
public sealed class ClearSwaggerOperationFilter : IOperationFilter
{
    private static readonly IReadOnlyDictionary<string, (string Group, string Noun)> Controllers =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["PharmacySuperAdminAuth"] = ("01 — Super Admin: Sign in and Security", "Super Admin"),
            ["PharmacySuperAdmin"] = ("02 — Super Admin: Manage Pharmacy Admins", "Pharmacy Admin"),
            ["PharmacyAdminAuth"] = ("03 — Pharmacy Admin: Sign in and Security", "Pharmacy Admin"),
            ["PharmacistAdmin"] = ("04 — Pharmacy Admin: Manage Pharmacists", "Pharmacist"),
            ["PharmacyAuth"] = ("05 — Pharmacist: Sign in and Password Reset", "Pharmacist"),
            ["Medicine"] = ("06 — Medicine Catalogue", "medicine"),
            ["MedicineImport"] = ("07 — Medicine CSV Import", "medicine import"),
            ["Inventory"] = ("08 — Inventory: Stock Overview", "inventory"),
            ["InventoryOperations"] = ("09 — Inventory: Batches and Adjustments", "inventory"),
            ["Supplier"] = ("10 — Suppliers", "supplier"),
            ["PurchaseOrder"] = ("11 — Purchase Orders", "purchase order"),
            ["DoctorPrescription"] = ("13 — Doctor Prescriptions", "prescription"),
            ["Pharmacy"] = ("14 — Pharmacist: Dispensing and Billing", "pharmacy operation"),
            ["BillingOperations"] = ("15 — Bills, Cancellation and Refunds", "bill"),
            ["Returns"] = ("16 — Medicine Returns", "medicine return"),
            ["StockTransfer"] = ("17 — Stock Transfers", "stock transfer"),
            ["PharmacyReports"] = ("18 — Dashboard, Reports and Audit", "report")
        };

    private static readonly IReadOnlyDictionary<string, string> ExactTitles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PharmacySuperAdmin.Branches"] = "List pharmacy branches",
            ["PharmacySuperAdmin.CreateAdmin"] = "Create an unassigned Pharmacy Admin",
            ["PharmacySuperAdmin.AssignPharmacy"] = "Assign a pharmacy to a Pharmacy Admin",
            ["PharmacySuperAdmin.ListAdmins"] = "List and search Pharmacy Admin accounts",
            ["PharmacySuperAdmin.GetAdmin"] = "Get one Pharmacy Admin account",
            ["PharmacySuperAdmin.UpdateAdmin"] = "Update a Pharmacy Admin account",
            ["PharmacySuperAdmin.ChangeStatus"] = "Activate or deactivate a Pharmacy Admin",
            ["PharmacySuperAdmin.ResetPassword"] = "Set a temporary password for a Pharmacy Admin",
            ["PharmacistAdmin.Permissions"] = "Update a Pharmacist's permissions",
            ["PharmacistAdmin.Create"] = "Create an unassigned Pharmacist",
            ["PharmacistAdmin.AssignPharmacy"] = "Assign the Admin's pharmacy to a Pharmacist",
            ["PharmacistAdmin.ResetPassword"] = "Set a temporary password for a Pharmacist",
            ["PharmacyAuth.ForgotPassword"] = "Send password-reset OTP to a Pharmacist",
            ["PharmacyAuth.VerifyResetOtp"] = "Verify Pharmacist OTP and issue a reset token",
            ["PharmacyAuth.ResetForgottenPassword"] = "Reset forgotten Pharmacist password",
            ["MedicineImport.DownloadTemplate"] = "Download the medicine-import CSV template",
            ["MedicineImport.Validate"] = "Validate a medicine CSV (legacy operation)",
            ["MedicineImport.Commit"] = "Commit a medicine CSV import (legacy operation)",
            ["Inventory.AddStock"] = "Add a medicine batch and increase stock",
            ["InventoryOperations.Levels"] = "Update minimum, maximum and reorder stock levels",
            ["InventoryOperations.Transactions"] = "View the stock transaction ledger",
            ["InventoryOperations.Adjust"] = "Increase or decrease stock with a reason",
            ["InventoryOperations.Dispose"] = "Dispose damaged or unusable batch stock",
            ["InventoryOperations.Quarantine"] = "Quarantine or release a medicine batch",
            ["PurchaseOrder.Receive"] = "Receive ordered batches and add them to stock",
            ["Pharmacy.GetPending"] = "List prescriptions waiting for the Pharmacist",
            ["Pharmacy.GetPrescription"] = "Get prescription details for dispensing",
            ["Pharmacy.Dispense"] = "Dispense prescribed medicines and reduce stock",
            ["Pharmacy.GenerateBill"] = "Generate a bill after dispensing",
            ["Pharmacy.PayBill"] = "Record payment for a pharmacy bill",
            ["Pharmacy.GetInvoice"] = "Get the pharmacy invoice",
            ["BillingOperations.Refund"] = "Refund a recorded payment",
            ["BillingOperations.CancelBill"] = "Cancel a pharmacy bill",
            ["StockTransfer.Dispatch"] = "Dispatch stock from the source branch",
            ["StockTransfer.Receive"] = "Receive stock at the destination branch",
            ["PharmacyReports.AuditLogs"] = "View user activity and audit logs"
        };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor action) return;

        var controller = action.ControllerName;
        var method = action.ActionName;
        (string Group, string Noun) info = Controllers.TryGetValue(controller, out var value)
            ? value
            : (Humanize(controller), Humanize(controller).ToLowerInvariant());
        var key = $"{controller}.{method}";
        var title = ExactTitles.TryGetValue(key, out var exact)
            ? exact
            : BuildTitle(method, info.Noun);

        operation.Tags = new List<OpenApiTag> { new() { Name = info.Group } };
        operation.Summary = title;
        operation.OperationId = $"{controller}_{method}";

        var endpoint = $"{context.ApiDescription.HttpMethod} /{context.ApiDescription.RelativePath}";
        operation.Description = $"**Purpose:** {title}.\n\n**Endpoint:** `{endpoint}`\n\n**Access:** {DescribeAccess(action.MethodInfo, action.ControllerTypeInfo)}";
    }

    private static string BuildTitle(string method, string noun) => method switch
    {
        "Login" => $"Sign in as {noun}",
        "Logout" => $"Sign out {noun}",
        "ChangePassword" => $"Change {noun} password while signed in",
        "Create" => $"Create a {noun}",
        "List" => $"List {noun} records",
        "Get" => $"Get one {noun} by ID",
        "Update" => $"Update a {noun}",
        "Status" => $"Change {noun} status",
        "Cancel" => $"Cancel a {noun}",
        "Approve" => $"Approve a {noun}",
        _ => Humanize(method)
    };

    private static string DescribeAccess(MethodInfo method, TypeInfo controller)
    {
        if (method.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            return "Public endpoint; no JWT is required.";

        var attributes = method.GetCustomAttributes<AuthorizeAttribute>(true)
            .Concat(controller.GetCustomAttributes<AuthorizeAttribute>(true)).ToArray();
        var roles = attributes.SelectMany(x => (x.Roles ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (roles.Length > 0)
            return $"Bearer JWT required. Allowed role(s): **{string.Join(", ", roles)}**.";
        return attributes.Length > 0
            ? "Bearer JWT required."
            : "Public endpoint; no JWT is required.";
    }

    private static string Humanize(string value) =>
        Regex.Replace(value, "([a-z0-9])([A-Z])", "$1 $2");
}
