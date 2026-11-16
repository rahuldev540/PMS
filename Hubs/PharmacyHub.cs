using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AuthDemo.Hubs;

[Authorize]
public class PharmacyHub : Hub
{
    public static string BranchGroup(int pharmacyId, int branchId) => $"pharmacy:{pharmacyId}:branch:{branchId}";

    public override async Task OnConnectedAsync()
    {
        var principal = Context.User;
        if (principal?.Identity?.IsAuthenticated != true ||
            !int.TryParse(principal.FindFirstValue("PharmacyId"), out var pharmacyId) || pharmacyId <= 0)
        {
            Context.Abort();
            return;
        }

        if (int.TryParse(principal.FindFirstValue("BranchId"), out var branchId) && branchId > 0)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BranchGroup(pharmacyId, branchId));
        }
        // Pharmacy administrators do not automatically join every branch group.
        // Explicit authorized subscriptions can be added in a later phase.
        await base.OnConnectedAsync();
    }
}
