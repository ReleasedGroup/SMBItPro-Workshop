using System.Net;
using System.Net.Http.Json;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;

namespace Helpdesk.Light.IntegrationTests;

public sealed class CustomerAndEndUserCrudIntegrationTests(HelpdeskApiFactory factory) : IClassFixture<HelpdeskApiFactory>
{
    [Fact]
    public async Task Admin_CanCrudCustomer_And_EndUsers()
    {
        using HttpClient adminClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(adminClient, SeedDataConstants.AdminEmail);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        string originalName = $"Crud Customer {suffix}";
        string updatedName = $"Crud Customer {suffix} Updated";
        string domain = $"crud-{suffix}.example";

        HttpResponseMessage createCustomerResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/admin/customers",
            new CreateCustomerRequest(originalName, true));

        createCustomerResponse.EnsureSuccessStatusCode();
        CustomerSummaryDto createdCustomer = (await createCustomerResponse.Content.ReadFromJsonAsync<CustomerSummaryDto>(TestAuth.JsonOptions))!;

        HttpResponseMessage addDomainResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/customers/{createdCustomer.Id}/domains",
            new AddCustomerDomainRequest(domain, true));

        addDomainResponse.EnsureSuccessStatusCode();

        HttpResponseMessage updateCustomerResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/admin/customers/{createdCustomer.Id}",
            new UpdateCustomerRequest(updatedName, false));

        updateCustomerResponse.EnsureSuccessStatusCode();
        CustomerSummaryDto updatedCustomer = (await updateCustomerResponse.Content.ReadFromJsonAsync<CustomerSummaryDto>(TestAuth.JsonOptions))!;
        Assert.Equal(updatedName, updatedCustomer.Name);
        Assert.False(updatedCustomer.IsActive);

        string endUserEmail = $"enduser-{suffix}@{domain}";

        HttpResponseMessage createEndUserResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/customers/{createdCustomer.Id}/end-users",
            new CreateEndUserRequest(endUserEmail, "End User One"));

        createEndUserResponse.EnsureSuccessStatusCode();
        EndUserSummaryDto createdEndUser = (await createEndUserResponse.Content.ReadFromJsonAsync<EndUserSummaryDto>(TestAuth.JsonOptions))!;
        Assert.Equal(endUserEmail, createdEndUser.Email);

        HttpResponseMessage listEndUsersResponse = await adminClient.GetAsync($"/api/v1/admin/customers/{createdCustomer.Id}/end-users");
        listEndUsersResponse.EnsureSuccessStatusCode();

        List<EndUserSummaryDto> endUsers = (await listEndUsersResponse.Content.ReadFromJsonAsync<List<EndUserSummaryDto>>(TestAuth.JsonOptions))!;
        Assert.Contains(endUsers, item => item.Id == createdEndUser.Id);

        HttpResponseMessage updateEndUserResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/admin/customers/{createdCustomer.Id}/end-users/{createdEndUser.Id}",
            new UpdateEndUserRequest(endUserEmail, "End User Updated", true));

        updateEndUserResponse.EnsureSuccessStatusCode();

        HttpResponseMessage deleteEndUserResponse = await adminClient.DeleteAsync(
            $"/api/v1/admin/customers/{createdCustomer.Id}/end-users/{createdEndUser.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteEndUserResponse.StatusCode);

        HttpResponseMessage deleteCustomerResponse = await adminClient.DeleteAsync($"/api/v1/admin/customers/{createdCustomer.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteCustomerResponse.StatusCode);

        HttpResponseMessage listCustomersResponse = await adminClient.GetAsync("/api/v1/admin/customers");
        listCustomersResponse.EnsureSuccessStatusCode();

        List<CustomerSummaryDto> customers = (await listCustomersResponse.Content.ReadFromJsonAsync<List<CustomerSummaryDto>>(TestAuth.JsonOptions))!;
        Assert.DoesNotContain(customers, item => item.Id == createdCustomer.Id);
    }

    [Fact]
    public async Task DeleteCustomer_WithDependentTickets_ReturnsConflict()
    {
        using HttpClient adminClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(adminClient, SeedDataConstants.AdminEmail);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        string domain = $"delete-conflict-{suffix}.example";

        CustomerSummaryDto customer = (await (await adminClient.PostAsJsonAsync(
            "/api/v1/admin/customers",
            new CreateCustomerRequest($"Delete Conflict {suffix}", true))).Content.ReadFromJsonAsync<CustomerSummaryDto>(TestAuth.JsonOptions))!;

        await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/customers/{customer.Id}/domains",
            new AddCustomerDomainRequest(domain, true));

        HttpResponseMessage createTicketResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/tickets",
            new CreateTicketRequest(
                customer.Id,
                "Ticket blocks customer delete",
                "Deleting this customer should be blocked while ticket exists.",
                TicketPriority.Medium,
                $"owner-{suffix}@{domain}"));

        createTicketResponse.EnsureSuccessStatusCode();

        HttpResponseMessage deleteCustomerResponse = await adminClient.DeleteAsync($"/api/v1/admin/customers/{customer.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteCustomerResponse.StatusCode);
    }
}
