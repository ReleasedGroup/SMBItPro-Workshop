namespace Helpdesk.Light.Application.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken, DateTime ExpiresUtc, Guid UserId, string Email, string Role, Guid? CustomerId);

public sealed record MeResponse(Guid UserId, string Email, string Role, Guid? CustomerId);
