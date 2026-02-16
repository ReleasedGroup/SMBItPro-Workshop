using Helpdesk.Light.Domain.Entities;

namespace Helpdesk.Light.UnitTests;

public sealed class TicketCategoryTests
{
    [Fact]
    public void Category_CanRenameToggleActiveAndMapResolverGroup()
    {
        TicketCategory category = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Network",
            isActive: true,
            resolverGroupId: null,
            createdUtc: DateTime.UtcNow);

        Guid resolverGroupId = Guid.NewGuid();
        category.Rename("Endpoint", DateTime.UtcNow);
        category.SetActive(false, DateTime.UtcNow);
        category.SetResolverGroup(resolverGroupId, DateTime.UtcNow);

        Assert.Equal("Endpoint", category.Name);
        Assert.False(category.IsActive);
        Assert.Equal(resolverGroupId, category.ResolverGroupId);
    }
}
