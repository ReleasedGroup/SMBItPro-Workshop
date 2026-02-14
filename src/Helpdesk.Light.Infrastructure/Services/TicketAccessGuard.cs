using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Abstractions.Tickets;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Domain.Tickets;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class TicketAccessGuard : ITicketAccessGuard
{
    public bool CanRead(Ticket ticket, TenantAccessContext context)
    {
        if (!context.IsAuthenticated)
        {
            return false;
        }

        if (context.IsMspAdmin)
        {
            return true;
        }

        if (!context.CustomerId.HasValue || context.CustomerId.Value != ticket.CustomerId)
        {
            return false;
        }

        if (context.Role == RoleNames.Technician)
        {
            return true;
        }

        if (context.Role == RoleNames.EndUser)
        {
            return context.UserId.HasValue && context.UserId.Value == ticket.CreatedByUserId;
        }

        return false;
    }

    public bool CanWrite(Ticket ticket, TenantAccessContext context)
    {
        if (!CanRead(ticket, context))
        {
            return false;
        }

        if (context.IsMspAdmin || context.Role == RoleNames.Technician)
        {
            return true;
        }

        return ticket.Status is not TicketStatus.Closed;
    }

    public bool CanManage(Ticket ticket, TenantAccessContext context)
    {
        if (!CanRead(ticket, context))
        {
            return false;
        }

        return context.IsMspAdmin || context.Role == RoleNames.Technician;
    }
}
