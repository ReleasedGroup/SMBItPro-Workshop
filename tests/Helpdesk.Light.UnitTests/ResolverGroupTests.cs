using Helpdesk.Light.Domain.Entities;

namespace Helpdesk.Light.UnitTests;

public sealed class ResolverGroupTests
{
    [Fact]
    public void RenameAndSetActive_UpdatesState()
    {
        ResolverGroup group = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Tier 1",
            true,
            DateTime.UtcNow);

        group.Rename("Tier 2", DateTime.UtcNow);
        group.SetActive(false, DateTime.UtcNow);

        Assert.Equal("Tier 2", group.Name);
        Assert.False(group.IsActive);
    }
}
