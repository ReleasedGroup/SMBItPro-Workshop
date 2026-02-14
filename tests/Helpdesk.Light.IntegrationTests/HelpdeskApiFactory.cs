using Helpdesk.Light.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Helpdesk.Light.IntegrationTests;

public sealed class HelpdeskApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string workspacePath = Path.Combine(Path.GetTempPath(), $"helpdesk-light-integration-{Guid.NewGuid():N}");

    private string DbPath => Path.Combine(workspacePath, "helpdesk.db");

    private string AttachmentsPath => Path.Combine(workspacePath, "attachments");

    private string ConnectionString => $"Data Source={DbPath}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:Helpdesk", ConnectionString);

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Helpdesk"] = ConnectionString,
                ["Attachments:RootPath"] = AttachmentsPath
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<HelpdeskDbContext>>();
            services.AddDbContext<HelpdeskDbContext>(options => options.UseSqlite(ConnectionString));
        });
    }

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(AttachmentsPath);

        if (File.Exists(DbPath))
        {
            File.Delete(DbPath);
        }

        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        if (Directory.Exists(workspacePath))
        {
            Directory.Delete(workspacePath, recursive: true);
        }
    }
}
