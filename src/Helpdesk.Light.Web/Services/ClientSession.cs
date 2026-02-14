using Helpdesk.Light.Application.Contracts;

namespace Helpdesk.Light.Web.Services;

public sealed class ClientSession
{
    public string AccessToken { get; private set; } = string.Empty;

    public Guid UserId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string Role { get; private set; } = string.Empty;

    public Guid? CustomerId { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);

    public event Action? Changed;

    public void SetLogin(LoginResponse response)
    {
        AccessToken = response.AccessToken;
        UserId = response.UserId;
        Email = response.Email;
        Role = response.Role;
        CustomerId = response.CustomerId;
        Changed?.Invoke();
    }

    public void Clear()
    {
        AccessToken = string.Empty;
        UserId = Guid.Empty;
        Email = string.Empty;
        Role = string.Empty;
        CustomerId = null;
        Changed?.Invoke();
    }
}
