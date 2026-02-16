using Helpdesk.Light.Domain.Tickets;

namespace Helpdesk.Light.UnitTests;

public sealed class TicketLifecycleTests
{
    [Fact]
    public void TransitionStatus_ValidFlow_Succeeds()
    {
        Ticket ticket = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            TicketChannel.Web,
            "Printer issue",
            "Printer queue is stalled.",
            TicketPriority.Medium,
            DateTime.UtcNow);

        ticket.TransitionStatus(TicketStatus.Triaged, DateTime.UtcNow);
        ticket.TransitionStatus(TicketStatus.InProgress, DateTime.UtcNow);
        ticket.TransitionStatus(TicketStatus.WaitingCustomer, DateTime.UtcNow);
        ticket.TransitionStatus(TicketStatus.InProgress, DateTime.UtcNow);
        ticket.TransitionStatus(TicketStatus.Resolved, DateTime.UtcNow);
        ticket.TransitionStatus(TicketStatus.Closed, DateTime.UtcNow);

        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Fact]
    public void TransitionStatus_InvalidFlow_ThrowsInvalidOperationException()
    {
        Ticket ticket = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            TicketChannel.Web,
            "Login issue",
            "Cannot sign in.",
            TicketPriority.Medium,
            DateTime.UtcNow);

        ticket.TransitionStatus(TicketStatus.Triaged, DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => ticket.TransitionStatus(TicketStatus.New, DateTime.UtcNow));
    }

    [Fact]
    public void BuildReferenceCode_UsesUppercaseHdPrefix()
    {
        Guid ticketId = Guid.Parse("4f8f4f1d-4d82-49b2-96b2-4b2468347634");

        string reference = Ticket.BuildReferenceCode(ticketId);

        Assert.Equal("HD-4F8F4F1D4D8249B296B24B2468347634", reference);
    }

    [Fact]
    public void Assign_CanSetResolverGroupAndClear()
    {
        Ticket ticket = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            TicketChannel.Web,
            "VPN issue",
            "Cannot connect to VPN.",
            TicketPriority.Medium,
            DateTime.UtcNow);

        Guid resolverGroupId = Guid.NewGuid();
        ticket.Assign(null, resolverGroupId, DateTime.UtcNow);

        Assert.Null(ticket.AssignedToUserId);
        Assert.Equal(resolverGroupId, ticket.ResolverGroupId);

        Guid technicianId = Guid.NewGuid();
        ticket.Assign(technicianId, null, DateTime.UtcNow);

        Assert.Equal(technicianId, ticket.AssignedToUserId);
        Assert.Null(ticket.ResolverGroupId);
    }
}
