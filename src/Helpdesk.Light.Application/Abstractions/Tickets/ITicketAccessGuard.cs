using Helpdesk.Light.Domain.Tickets;

namespace Helpdesk.Light.Application.Abstractions.Tickets;

public interface ITicketAccessGuard
{
    bool CanRead(Ticket ticket, TenantAccessContext context);

    bool CanWrite(Ticket ticket, TenantAccessContext context);

    bool CanManage(Ticket ticket, TenantAccessContext context);
}
