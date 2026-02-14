using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Helpdesk.Light.Web;
using Helpdesk.Light.Web.Services;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

string apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5035/";
if (!apiBaseUrl.EndsWith("/", StringComparison.Ordinal))
{
    apiBaseUrl += "/";
}

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute)
});
builder.Services.AddSingleton<ClientSession>();
builder.Services.AddScoped<HelpdeskApiClient>();

await builder.Build().RunAsync();
