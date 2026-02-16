using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Abstractions.Email;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Domain.Email;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Options;
using Helpdesk.Light.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Helpdesk.Light.UnitTests;

public sealed class OutboundEmailServiceTests
{
    [Fact]
    public async Task DispatchPendingAsync_WhenTransportFails_AttemptsOnlyOncePerRun()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using HelpdeskDbContext dbContext = CreateDbContext(connection);

        OutboundEmailMessage message = new(
            Guid.NewGuid(),
            null,
            SeedDataConstants.ContosoCustomerId,
            "enduser@contoso.com",
            "Retry once",
            "Body",
            $"retry-once:{Guid.NewGuid():N}",
            DateTime.UtcNow);

        dbContext.OutboundEmailMessages.Add(message);
        await dbContext.SaveChangesAsync();

        FailingEmailTransport transport = new();
        TestRuntimeMetricsRecorder metrics = new();
        OutboundEmailService service = CreateService(dbContext, transport, metrics, maxRetryCount: 3);

        await service.DispatchPendingAsync();

        OutboundEmailMessage persisted = await dbContext.OutboundEmailMessages.SingleAsync(item => item.Id == message.Id);
        int deadLetterEvents = await dbContext.AuditEvents.CountAsync(item => item.EventType == "email.dead_lettered");

        Assert.Equal(1, transport.SendCount);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Equal(OutboundEmailStatus.Failed, persisted.Status);
        Assert.Equal(1, metrics.EmailFailedCount);
        Assert.Equal(0, metrics.EmailDeadLetterCount);
        Assert.Equal(0, deadLetterEvents);
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenFinalAttemptFails_MarksDeadLetterOnce()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using HelpdeskDbContext dbContext = CreateDbContext(connection);

        OutboundEmailMessage message = new(
            Guid.NewGuid(),
            null,
            SeedDataConstants.ContosoCustomerId,
            "enduser@contoso.com",
            "Final attempt",
            "Body",
            $"final-attempt:{Guid.NewGuid():N}",
            DateTime.UtcNow);

        message.MarkAttempt();
        message.MarkFailure("smtp timeout #1");
        message.MarkAttempt();
        message.MarkFailure("smtp timeout #2");

        dbContext.OutboundEmailMessages.Add(message);
        await dbContext.SaveChangesAsync();

        FailingEmailTransport transport = new();
        TestRuntimeMetricsRecorder metrics = new();
        OutboundEmailService service = CreateService(dbContext, transport, metrics, maxRetryCount: 3);

        await service.DispatchPendingAsync();

        OutboundEmailMessage persisted = await dbContext.OutboundEmailMessages.SingleAsync(item => item.Id == message.Id);
        int deadLetterEvents = await dbContext.AuditEvents.CountAsync(item => item.EventType == "email.dead_lettered");

        Assert.Equal(1, transport.SendCount);
        Assert.Equal(3, persisted.AttemptCount);
        Assert.Equal(OutboundEmailStatus.DeadLetter, persisted.Status);
        Assert.Equal(1, metrics.EmailDeadLetterCount);
        Assert.Equal(1, deadLetterEvents);
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenRetryBudgetAlreadySpent_DoesNotSendAgain()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using HelpdeskDbContext dbContext = CreateDbContext(connection);

        OutboundEmailMessage message = new(
            Guid.NewGuid(),
            null,
            SeedDataConstants.ContosoCustomerId,
            "enduser@contoso.com",
            "Already exhausted",
            "Body",
            $"already-exhausted:{Guid.NewGuid():N}",
            DateTime.UtcNow);

        message.MarkAttempt();
        message.MarkFailure("smtp timeout #1");
        message.MarkAttempt();
        message.MarkFailure("smtp timeout #2");
        message.MarkAttempt();
        message.MarkFailure("smtp timeout #3");

        dbContext.OutboundEmailMessages.Add(message);
        await dbContext.SaveChangesAsync();

        FailingEmailTransport transport = new();
        TestRuntimeMetricsRecorder metrics = new();
        OutboundEmailService service = CreateService(dbContext, transport, metrics, maxRetryCount: 3);

        await service.DispatchPendingAsync();

        OutboundEmailMessage persisted = await dbContext.OutboundEmailMessages.SingleAsync(item => item.Id == message.Id);
        int deadLetterEvents = await dbContext.AuditEvents.CountAsync(item => item.EventType == "email.dead_lettered");

        Assert.Equal(0, transport.SendCount);
        Assert.Equal(3, persisted.AttemptCount);
        Assert.Equal(OutboundEmailStatus.DeadLetter, persisted.Status);
        Assert.Equal(1, metrics.EmailDeadLetterCount);
        Assert.Equal(1, deadLetterEvents);
    }

    private static OutboundEmailService CreateService(
        HelpdeskDbContext dbContext,
        IEmailTransport transport,
        IRuntimeMetricsRecorder metrics,
        int maxRetryCount)
    {
        TestTenantContextAccessor tenantContext = new(new TenantAccessContext(
            Guid.NewGuid(),
            "admin@msp.local",
            RoleNames.MspAdmin,
            null));

        return new OutboundEmailService(
            dbContext,
            transport,
            tenantContext,
            metrics,
            Options.Create(new EmailOptions { MaxRetryCount = maxRetryCount }));
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

    private sealed class FailingEmailTransport : IEmailTransport
    {
        public int SendCount { get; private set; }

        public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default)
        {
            SendCount += 1;
            throw new InvalidOperationException("smtp transport unavailable");
        }
    }

    private sealed class TestTenantContextAccessor(TenantAccessContext context) : ITenantContextAccessor
    {
        public TenantAccessContext Current => context;
    }

    private sealed class TestRuntimeMetricsRecorder : IRuntimeMetricsRecorder
    {
        public int EmailFailedCount { get; private set; }

        public int EmailDeadLetterCount { get; private set; }

        public void RecordApiRequest(int statusCode, double latencyMs)
        {
        }

        public void SetWorkerQueueDepth(int depth)
        {
        }

        public void RecordEmailSent()
        {
        }

        public void RecordEmailFailed()
        {
            EmailFailedCount += 1;
        }

        public void RecordEmailDeadLetter()
        {
            EmailDeadLetterCount += 1;
        }

        public void RecordAiRun(double durationMs, bool success)
        {
        }

        public void RecordWorkerFailure(string worker, string error)
        {
        }

        public OperationsMetricsDto GetSnapshot()
        {
            return new OperationsMetricsDto(
                DateTime.UtcNow,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                []);
        }
    }
}
