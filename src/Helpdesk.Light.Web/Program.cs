using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Helpdesk.Light.Web;
using Helpdesk.Light.Web.Services;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

string apiBaseUrl = ResolveApiBaseUrl(builder.Configuration["ApiBaseUrl"], builder.HostEnvironment.BaseAddress);

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute)
});
builder.Services.AddSingleton<ClientSession>();
builder.Services.AddScoped<HelpdeskApiClient>();

await builder.Build().RunAsync();

static string ResolveApiBaseUrl(string? configuredApiBaseUrl, string hostBaseAddress)
{
    if (!string.IsNullOrWhiteSpace(configuredApiBaseUrl))
    {
        return EnsureTrailingSlash(configuredApiBaseUrl);
    }

    Uri hostUri = new(hostBaseAddress, UriKind.Absolute);
    if (hostUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
    {
        return hostUri.Port switch
        {
            5006 or 7262 => "http://localhost:5283/",
            8082 => "http://localhost:8080/",
            _ => EnsureTrailingSlash(hostUri.GetLeftPart(UriPartial.Authority))
        };
    }

    return EnsureTrailingSlash(hostUri.GetLeftPart(UriPartial.Authority));
}

static string EnsureTrailingSlash(string value)
{
    string normalized = value.Trim();
    if (!normalized.EndsWith("/", StringComparison.Ordinal))
    {
        normalized += "/";
    }

    return normalized;
}
