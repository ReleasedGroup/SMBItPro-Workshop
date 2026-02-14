using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Entities;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.UnitTests;

public sealed class CustomerAdministrationServiceTests
{
    [Fact]
    public async Task GetCustomerAsync_WhenTenantDoesNotMatch_ThrowsTenantAccessDeniedException()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<HelpdeskDbContext> options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseSqlite(connection)
            .Options;

        await using HelpdeskDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        Customer contoso = new(SeedDataConstants.ContosoCustomerId, "Contoso");
        dbContext.Customers.Add(contoso);
        await dbContext.SaveChangesAsync();

        TestTenantContextAccessor tenantContext = new(new TenantAccessContext(
            Guid.NewGuid(),
            "tech@fabrikam.com",
            "Technician",
            SeedDataConstants.FabrikamCustomerId));

        CustomerAdministrationService service = new(dbContext, tenantContext);

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => service.GetCustomerAsync(SeedDataConstants.ContosoCustomerId));
    }

    [Fact]
    public async Task GetCustomerAsync_WhenAdmin_CanAccessAnyCustomer()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<HelpdeskDbContext> options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseSqlite(connection)
            .Options;

        await using HelpdeskDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        Customer contoso = new(SeedDataConstants.ContosoCustomerId, "Contoso");
        contoso.AddDomain(Guid.NewGuid(), "contoso.com", true);

        dbContext.Customers.Add(contoso);
        await dbContext.SaveChangesAsync();

        TestTenantContextAccessor tenantContext = new(new TenantAccessContext(
            Guid.NewGuid(),
            "admin@msp.local",
            "MspAdmin",
            null));

        CustomerAdministrationService service = new(dbContext, tenantContext);

        var customer = await service.GetCustomerAsync(SeedDataConstants.ContosoCustomerId);

        Assert.NotNull(customer);
        Assert.Equal("Contoso", customer!.Name);
        Assert.Single(customer.Domains);
    }

    private sealed class TestTenantContextAccessor(TenantAccessContext context) : ITenantContextAccessor
    {
        public TenantAccessContext Current => context;
    }
}
