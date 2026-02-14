using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Services;

namespace Helpdesk.Light.UnitTests;

public sealed class TicketAccessGuardTests
{
    [Fact]
    public void EndUser_CanReadOwnTicket_ButCannotManage()
    {
        Guid customerId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        Ticket ticket = new(Guid.NewGuid(), customerId, userId, TicketChannel.Web, "Issue", "Details", TicketPriority.Low, DateTime.UtcNow);
        TicketAccessGuard guard = new();

        TenantAccessContext context = new(userId, "user@contoso.com", RoleNames.EndUser, customerId);

        Assert.True(guard.CanRead(ticket, context));
        Assert.True(guard.CanWrite(ticket, context));
        Assert.False(guard.CanManage(ticket, context));
    }

    [Fact]
    public void EndUser_CannotReadAnotherUsersTicket()
    {
        Guid customerId = Guid.NewGuid();

        Ticket ticket = new(Guid.NewGuid(), customerId, Guid.NewGuid(), TicketChannel.Web, "Issue", "Details", TicketPriority.Low, DateTime.UtcNow);
        TicketAccessGuard guard = new();

        TenantAccessContext context = new(Guid.NewGuid(), "user@contoso.com", RoleNames.EndUser, customerId);

        Assert.False(guard.CanRead(ticket, context));
    }

    [Fact]
    public void Technician_CanManageSameTenantTicket()
    {
        Guid customerId = Guid.NewGuid();

        Ticket ticket = new(Guid.NewGuid(), customerId, Guid.NewGuid(), TicketChannel.Web, "Issue", "Details", TicketPriority.Low, DateTime.UtcNow);
        TicketAccessGuard guard = new();

        TenantAccessContext context = new(Guid.NewGuid(), "tech@contoso.com", RoleNames.Technician, customerId);

        Assert.True(guard.CanRead(ticket, context));
        Assert.True(guard.CanWrite(ticket, context));
        Assert.True(guard.CanManage(ticket, context));
    }

    [Fact]
    public void Admin_CanManageAnyTicket()
    {
        Ticket ticket = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TicketChannel.Web, "Issue", "Details", TicketPriority.Low, DateTime.UtcNow);
        TicketAccessGuard guard = new();

        TenantAccessContext context = new(Guid.NewGuid(), "admin@msp.local", RoleNames.MspAdmin, null);

        Assert.True(guard.CanRead(ticket, context));
        Assert.True(guard.CanWrite(ticket, context));
        Assert.True(guard.CanManage(ticket, context));
    }
}
