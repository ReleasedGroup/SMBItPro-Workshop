using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.Light.Infrastructure.Data;

namespace Helpdesk.Light.IntegrationTests;

internal static class TestAuth
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<LoginResponse> LoginAsync(HttpClient client, string email)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = SeedDataConstants.DefaultPassword
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions))!;
    }

    public static async Task<LoginResponse> LoginAndSetAuthHeaderAsync(HttpClient client, string email)
    {
        LoginResponse login = await LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return login;
    }

    internal sealed record LoginResponse(string AccessToken, DateTime ExpiresUtc, Guid UserId, string Email, string Role, Guid? CustomerId);
}
