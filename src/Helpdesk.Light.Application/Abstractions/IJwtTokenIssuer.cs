namespace Helpdesk.Light.Application.Abstractions;

public interface IJwtTokenIssuer
{
    string IssueToken(Guid userId, string email, string role, Guid? customerId, out DateTime expiresUtc);
}
