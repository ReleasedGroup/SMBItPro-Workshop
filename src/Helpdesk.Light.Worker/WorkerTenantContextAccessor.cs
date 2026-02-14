using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Domain.Security;

namespace Helpdesk.Light.Worker;

public sealed class WorkerTenantContextAccessor : ITenantContextAccessor
{
    private static readonly TenantAccessContext WorkerContext = new(
        Guid.Parse("7e1d995f-d774-4e5b-a6be-f8f1981f3d19"),
        "worker@system.local",
        RoleNames.MspAdmin,
        null);

    public TenantAccessContext Current => WorkerContext;
}
