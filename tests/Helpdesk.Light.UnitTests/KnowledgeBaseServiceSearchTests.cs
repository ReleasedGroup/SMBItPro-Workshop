using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Domain.Ai;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Helpdesk.Light.UnitTests;

public sealed class KnowledgeBaseServiceSearchTests
{
    [Fact]
    public async Task ListAsync_SearchTreatsPercentAndUnderscoreAsLiteralCharacters()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using HelpdeskDbContext dbContext = CreateDbContext(connection);

        Guid customerId = SeedDataConstants.ContosoCustomerId;
        Guid editorId = Guid.NewGuid();
        DateTime utcNow = DateTime.UtcNow;

        KnowledgeArticle percentArticle = new(
            Guid.NewGuid(),
            customerId,
            null,
            "CPU at 100% usage",
            "Escalation note",
            aiGenerated: false,
            editedByUserId: editorId,
            createdUtc: utcNow);

        KnowledgeArticle underscoreArticle = new(
            Guid.NewGuid(),
            customerId,
            null,
            "Queue_delay troubleshooting",
            "Escalation note",
            aiGenerated: false,
            editedByUserId: editorId,
            createdUtc: utcNow.AddMinutes(1));

        KnowledgeArticle normalArticle = new(
            Guid.NewGuid(),
            customerId,
            null,
            "General troubleshooting guide",
            "Escalation note",
            aiGenerated: false,
            editedByUserId: editorId,
            createdUtc: utcNow.AddMinutes(2));

        dbContext.KnowledgeArticles.AddRange(percentArticle, underscoreArticle, normalArticle);
        await dbContext.SaveChangesAsync();

        TestTenantContextAccessor tenantContext = new(new TenantAccessContext(
            editorId,
            "tech@contoso.com",
            RoleNames.Technician,
            customerId));

        KnowledgeBaseService service = new(
            dbContext,
            tenantContext,
            new StubPlatformSettingsService(),
            NullLogger<KnowledgeBaseService>.Instance);

        IReadOnlyList<KnowledgeArticleSummaryDto> percentResults = await service.ListAsync(
            new KnowledgeArticleListRequest("%", null, customerId, 50));

        Assert.Contains(percentResults, item => item.Id == percentArticle.Id);
        Assert.DoesNotContain(percentResults, item => item.Id == underscoreArticle.Id);
        Assert.DoesNotContain(percentResults, item => item.Id == normalArticle.Id);

        IReadOnlyList<KnowledgeArticleSummaryDto> underscoreResults = await service.ListAsync(
            new KnowledgeArticleListRequest("_", null, customerId, 50));

        Assert.Contains(underscoreResults, item => item.Id == underscoreArticle.Id);
        Assert.DoesNotContain(underscoreResults, item => item.Id == percentArticle.Id);
        Assert.DoesNotContain(underscoreResults, item => item.Id == normalArticle.Id);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static HelpdeskDbContext CreateDbContext(SqliteConnection connection)
    {
        DbContextOptions<HelpdeskDbContext> options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseSqlite(connection)
            .Options;

        HelpdeskDbContext dbContext = new(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private sealed class TestTenantContextAccessor(TenantAccessContext context) : ITenantContextAccessor
    {
        public TenantAccessContext Current => context;
    }

    private sealed class StubPlatformSettingsService : IPlatformSettingsService
    {
        public Task<PlatformSettingsDto> GetAdminSettingsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PlatformSettingsDto> UpdateAdminSettingsAsync(
            PlatformSettingsUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RuntimePlatformSettings> GetRuntimeSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RuntimePlatformSettings(
                EnableAi: false,
                ModelId: "gpt-5-mini",
                OpenAIApiKey: null,
                EmailTransportMode: EmailTransportModes.Console,
                Smtp: new RuntimeSmtpSettings(
                    Host: string.Empty,
                    Port: 587,
                    UseSsl: true,
                    Username: string.Empty,
                    Password: string.Empty,
                    FromAddress: "noreply@example.com",
                    FromDisplayName: "Helpdesk"),
                Graph: new RuntimeGraphEmailSettings(
                    TenantId: string.Empty,
                    ClientId: string.Empty,
                    ClientSecret: string.Empty,
                    SenderUserId: string.Empty)));
        }
    }
}
