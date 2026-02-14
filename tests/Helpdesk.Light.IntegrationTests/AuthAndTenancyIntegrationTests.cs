using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.Light.Infrastructure.Data;

namespace Helpdesk.Light.IntegrationTests;

public sealed class AuthAndTenancyIntegrationTests(HelpdeskApiFactory factory) : IClassFixture<HelpdeskApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Login_WithSeededAdmin_ReturnsJwtAndRole()
    {
        using HttpClient client = factory.CreateClient();

        LoginResponse? response = await LoginAsync(client, SeedDataConstants.AdminEmail);

        Assert.NotNull(response);
        Assert.Equal("MspAdmin", response!.Role);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
    }

    [Fact]
    public async Task AdminCustomerManagement_CreateCustomerAndDomain_Succeeds()
    {
        using HttpClient client = factory.CreateClient();
        string token = (await LoginAsync(client, SeedDataConstants.AdminEmail))!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/v1/admin/customers", new
        {
            name = $"Northwind-{suffix}",
            isActive = true
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        CustomerSummary? createdCustomer = await createResponse.Content.ReadFromJsonAsync<CustomerSummary>(JsonOptions);
        Assert.NotNull(createdCustomer);

        HttpResponseMessage addDomainResponse = await client.PostAsJsonAsync($"/api/v1/admin/customers/{createdCustomer!.Id}/domains", new
        {
            domain = $"northwind-{suffix}.com",
            isPrimary = true
        });

        Assert.Equal(HttpStatusCode.Created, addDomainResponse.StatusCode);

        HttpResponseMessage listResponse = await client.GetAsync("/api/v1/admin/customers");
        listResponse.EnsureSuccessStatusCode();

        List<CustomerSummary>? allCustomers = await listResponse.Content.ReadFromJsonAsync<List<CustomerSummary>>(JsonOptions);
        Assert.NotNull(allCustomers);
        Assert.Contains(allCustomers!, item => item.Id == createdCustomer.Id);
    }

    [Fact]
    public async Task Technician_CannotAccessAnotherTenantCustomerData()
    {
        using HttpClient client = factory.CreateClient();
        string token = (await LoginAsync(client, SeedDataConstants.ContosoTechEmail))!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage forbiddenResponse = await client.GetAsync($"/api/v1/customers/{SeedDataConstants.FabrikamCustomerId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        HttpResponseMessage allowedResponse = await client.GetAsync($"/api/v1/customers/{SeedDataConstants.ContosoCustomerId}");
        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
    }

    [Fact]
    public async Task Technician_CannotCallAdminCustomersEndpoint()
    {
        using HttpClient client = factory.CreateClient();
        string token = (await LoginAsync(client, SeedDataConstants.ContosoTechEmail))!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/customers");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TenantResolution_MappedAndUnmapped_AreHandled()
    {
        using HttpClient client = factory.CreateClient();
        string token = (await LoginAsync(client, SeedDataConstants.AdminEmail))!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage mappedResponse = await client.PostAsJsonAsync("/api/v1/tenant-resolution/resolve", new
        {
            senderEmail = "user@contoso.com",
            subject = "Mapped"
        });

        mappedResponse.EnsureSuccessStatusCode();
        TenantResolutionResponse? mapped = await mappedResponse.Content.ReadFromJsonAsync<TenantResolutionResponse>(JsonOptions);

        Assert.NotNull(mapped);
        Assert.True(mapped!.IsMapped);
        Assert.Equal(SeedDataConstants.ContosoCustomerId, mapped.CustomerId);

        HttpResponseMessage unmappedResponse = await client.PostAsJsonAsync("/api/v1/tenant-resolution/resolve", new
        {
            senderEmail = "unknown@unmappeddomain.io",
            subject = "Unmapped"
        });

        unmappedResponse.EnsureSuccessStatusCode();
        TenantResolutionResponse? unmapped = await unmappedResponse.Content.ReadFromJsonAsync<TenantResolutionResponse>(JsonOptions);

        Assert.NotNull(unmapped);
        Assert.False(unmapped!.IsMapped);
        Assert.NotNull(unmapped.UnmappedQueueItemId);

        HttpResponseMessage queueResponse = await client.GetAsync("/api/v1/tenant-resolution/unmapped");
        queueResponse.EnsureSuccessStatusCode();

        List<UnmappedQueueItem>? queue = await queueResponse.Content.ReadFromJsonAsync<List<UnmappedQueueItem>>(JsonOptions);
        Assert.NotNull(queue);
        Assert.Contains(queue!, item => item.Id == unmapped.UnmappedQueueItemId);
    }

    private static async Task<LoginResponse?> LoginAsync(HttpClient client, string email)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = SeedDataConstants.DefaultPassword
        });

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
    }

    private sealed record LoginResponse(string AccessToken, DateTime ExpiresUtc, Guid UserId, string Email, string Role, Guid? CustomerId);

    private sealed record CustomerSummary(Guid Id, string Name, bool IsActive, int DomainCount);

    private sealed record TenantResolutionResponse(bool IsMapped, Guid? CustomerId, string? CustomerName, string SenderDomain, Guid? UnmappedQueueItemId);

    private sealed record UnmappedQueueItem(Guid Id, string SenderEmail, string SenderDomain, string Subject, DateTime ReceivedUtc);
}
