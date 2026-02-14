namespace Helpdesk.Light.Application.Errors;

public sealed class TenantAccessDeniedException(string message) : Exception(message)
{
}
