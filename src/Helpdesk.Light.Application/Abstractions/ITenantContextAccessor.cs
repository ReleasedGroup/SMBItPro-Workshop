namespace Helpdesk.Light.Application.Abstractions;

public interface ITenantContextAccessor
{
    TenantAccessContext Current { get; }
}
