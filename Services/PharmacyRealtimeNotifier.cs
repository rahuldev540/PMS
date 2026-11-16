using AuthDemo.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AuthDemo.Services;

public interface IPharmacyRealtimeNotifier
{
    Task PublishAsync(int pharmacyId, int branchId, string eventName, object payload, CancellationToken ct = default);
}

public sealed class PharmacyRealtimeNotifier : IPharmacyRealtimeNotifier
{
    private readonly IHubContext<PharmacyHub> _hub;
    public PharmacyRealtimeNotifier(IHubContext<PharmacyHub> hub) => _hub = hub;

    public Task PublishAsync(int pharmacyId, int branchId, string eventName, object payload, CancellationToken ct = default)
    {
        if (pharmacyId <= 0 || branchId <= 0)
            throw new ArgumentOutOfRangeException(nameof(pharmacyId), "Pharmacy and branch identifiers must be positive.");
        return _hub.Clients.Group(PharmacyHub.BranchGroup(pharmacyId, branchId))
            .SendAsync(eventName, new { PharmacyId = pharmacyId, BranchId = branchId, Data = payload }, ct);
    }
}
