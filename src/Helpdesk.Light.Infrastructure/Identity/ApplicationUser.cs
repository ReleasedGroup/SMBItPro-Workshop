using Microsoft.AspNetCore.Identity;

namespace Helpdesk.Light.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }
}
