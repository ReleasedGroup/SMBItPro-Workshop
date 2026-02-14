using System.Security.Claims;
using Helpdesk.Light.Application.Abstractions;

namespace Helpdesk.Light.Api.Tenancy;

public sealed class HttpTenantContextAccessor(IHttpContextAccessor httpContextAccessor) : ITenantContextAccessor
{
    public TenantAccessContext Current
    {
        get
        {
            ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return TenantAccessContext.Anonymous;
            }

            Guid? userId = TryReadGuid(user, ClaimTypes.NameIdentifier);
            string email = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            string role = user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            Guid? customerId = TryReadGuid(user, ClaimTypesExtension.CustomerId);

            return new TenantAccessContext(userId, email, role, customerId);
        }
    }

    private static Guid? TryReadGuid(ClaimsPrincipal user, string claimType)
    {
        string? raw = user.FindFirstValue(claimType);
        return Guid.TryParse(raw, out Guid value) ? value : null;
    }
}
