using System.Net;
using System.Net.Http.Json;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Infrastructure.Data;

namespace Helpdesk.Light.IntegrationTests;

public sealed class PlatformSettingsIntegrationTests(HelpdeskApiFactory factory) : IClassFixture<HelpdeskApiFactory>
{
    [Fact]
    public async Task MspAdmin_CanUpdateAndReadPlatformSettings()
    {
        using HttpClient client = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(client, SeedDataConstants.AdminEmail);

        HttpResponseMessage initialResponse = await client.GetAsync("/api/v1/admin/settings");
        initialResponse.EnsureSuccessStatusCode();

        PlatformSettingsDto initial = (await initialResponse.Content.ReadFromJsonAsync<PlatformSettingsDto>(TestAuth.JsonOptions))!;
        Assert.Equal("gpt-5.2", initial.ModelId);

        PlatformSettingsUpdateRequest update = new(
            EnableAi: true,
            EmailTransportMode: EmailTransportModes.Smtp,
            OpenAIApiKey: "sk-test-value",
            ClearOpenAIApiKey: false,
            Smtp: new SmtpSettingsUpdateRequest(
                Host: "smtp.example.com",
                Port: 587,
                UseSsl: true,
                Username: "mailer@example.com",
                FromAddress: "helpdesk@example.com",
                FromDisplayName: "Helpdesk",
                Password: "smtp-secret",
                ClearPassword: false),
            Graph: new GraphEmailSettingsUpdateRequest(
                TenantId: "tenant-id",
                ClientId: "client-id",
                SenderUserId: "helpdesk@example.com",
                ClientSecret: "graph-secret",
                ClearClientSecret: false));

        HttpResponseMessage updateResponse = await client.PutAsJsonAsync("/api/v1/admin/settings", update);
        updateResponse.EnsureSuccessStatusCode();

        PlatformSettingsDto updated = (await updateResponse.Content.ReadFromJsonAsync<PlatformSettingsDto>(TestAuth.JsonOptions))!;
        Assert.True(updated.HasOpenAIApiKey);
        Assert.Equal(EmailTransportModes.Smtp, updated.EmailTransportMode);
        Assert.Equal("smtp.example.com", updated.Smtp.Host);
        Assert.Equal(587, updated.Smtp.Port);
        Assert.True(updated.Smtp.HasPassword);
        Assert.True(updated.Graph.HasClientSecret);

        PlatformSettingsUpdateRequest clearSecrets = new(
            EnableAi: true,
            EmailTransportMode: EmailTransportModes.Graph,
            OpenAIApiKey: null,
            ClearOpenAIApiKey: true,
            Smtp: new SmtpSettingsUpdateRequest(
                Host: "smtp.example.com",
                Port: 587,
                UseSsl: true,
                Username: "mailer@example.com",
                FromAddress: "helpdesk@example.com",
                FromDisplayName: "Helpdesk",
                Password: null,
                ClearPassword: true),
            Graph: new GraphEmailSettingsUpdateRequest(
                TenantId: "tenant-id",
                ClientId: "client-id",
                SenderUserId: "helpdesk@example.com",
                ClientSecret: null,
                ClearClientSecret: true));

        HttpResponseMessage clearResponse = await client.PutAsJsonAsync("/api/v1/admin/settings", clearSecrets);
        clearResponse.EnsureSuccessStatusCode();
        PlatformSettingsDto cleared = (await clearResponse.Content.ReadFromJsonAsync<PlatformSettingsDto>(TestAuth.JsonOptions))!;
        Assert.False(cleared.HasOpenAIApiKey);
        Assert.False(cleared.Smtp.HasPassword);
        Assert.False(cleared.Graph.HasClientSecret);
        Assert.Equal(EmailTransportModes.Graph, cleared.EmailTransportMode);
    }

    [Fact]
    public async Task Technician_CannotAccessAdminSettings()
    {
        using HttpClient client = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(client, SeedDataConstants.ContosoTechEmail);

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/settings");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
