using Helpdesk.Light.Domain.Security;

namespace Helpdesk.Light.Application.Abstractions;

public sealed record TenantAccessContext(Guid? UserId, string Email, string Role, Guid? CustomerId)
{
    public static readonly TenantAccessContext Anonymous = new(null, string.Empty, string.Empty, null);

    public bool IsAuthenticated => UserId.HasValue;

    public bool IsMspAdmin => Role.Equals(RoleNames.MspAdmin, StringComparison.Ordinal);

    public bool CanAccessCustomer(Guid customerId)
    {
        return IsMspAdmin || (CustomerId.HasValue && CustomerId.Value == customerId);
    }
}
