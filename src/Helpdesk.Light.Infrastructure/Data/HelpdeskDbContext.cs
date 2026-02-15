using Helpdesk.Light.Domain.Ai;
using Helpdesk.Light.Domain.Email;
using Helpdesk.Light.Domain.Entities;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Helpdesk.Light.Infrastructure.Data;

public sealed class HelpdeskDbContext(DbContextOptions<HelpdeskDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerDomain> CustomerDomains => Set<CustomerDomain>();

    public DbSet<UnmappedInboundItem> UnmappedInboundItems => Set<UnmappedInboundItem>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();

    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<OutboundEmailMessage> OutboundEmailMessages => Set<OutboundEmailMessage>();

    public DbSet<AiRun> AiRuns => Set<AiRun>();

    public DbSet<TicketAiSuggestion> TicketAiSuggestions => Set<TicketAiSuggestion>();

    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ValueConverter<TicketStatus, string> ticketStatusConverter = new(
            value => value.ToString(),
            value => Enum.Parse<TicketStatus>(value));

        ValueConverter<TicketPriority, string> ticketPriorityConverter = new(
            value => value.ToString(),
            value => Enum.Parse<TicketPriority>(value));

        ValueConverter<TicketChannel, string> ticketChannelConverter = new(
            value => value.ToString(),
            value => Enum.Parse<TicketChannel>(value));

        ValueConverter<TicketAuthorType, string> authorTypeConverter = new(
            value => value.ToString(),
            value => Enum.Parse<TicketAuthorType>(value));

        ValueConverter<TicketMessageSource, string> messageSourceConverter = new(
            value => value.ToString(),
            value => Enum.Parse<TicketMessageSource>(value));

        ValueConverter<OutboundEmailStatus, string> emailStatusConverter = new(
            value => value.ToString(),
            value => Enum.Parse<OutboundEmailStatus>(value));

        ValueConverter<AiSuggestionStatus, string> aiSuggestionStatusConverter = new(
            value => value.ToString(),
            value => Enum.Parse<AiSuggestionStatus>(value));

        ValueConverter<AiPolicyMode, string> aiPolicyModeConverter = new(
            value => value.ToString(),
            value => Enum.Parse<AiPolicyMode>(value));

        ValueConverter<KnowledgeArticleStatus, string> knowledgeArticleStatusConverter = new(
            value => value.ToString(),
            value => Enum.Parse<KnowledgeArticleStatus>(value));

        builder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.IsActive).IsRequired();
            entity.Property(item => item.AiPolicyMode)
                .HasConversion(aiPolicyModeConverter)
                .HasMaxLength(40)
                .IsRequired();
            entity.Property(item => item.AutoRespondMinConfidence).IsRequired();
            entity.HasMany(item => item.Domains)
                .WithOne(item => item.Customer)
                .HasForeignKey(item => item.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CustomerDomain>(entity =>
        {
            entity.ToTable("CustomerDomains");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Domain).HasMaxLength(320).IsRequired();
            entity.HasIndex(item => item.Domain).IsUnique();
            entity.HasIndex(item => new { item.CustomerId, item.Domain }).IsUnique();
        });

        builder.Entity<UnmappedInboundItem>(entity =>
        {
            entity.ToTable("UnmappedInboundItems");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SenderEmail).HasMaxLength(320).IsRequired();
            entity.Property(item => item.SenderDomain).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(400).IsRequired();
            entity.Property(item => item.ReceivedUtc).IsRequired();
            entity.HasIndex(item => item.ReceivedUtc);
        });

        builder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Tickets");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ReferenceCode).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Channel).HasConversion(ticketChannelConverter).HasMaxLength(16).IsRequired();
            entity.Property(item => item.Status).HasConversion(ticketStatusConverter).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Priority).HasConversion(ticketPriorityConverter).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(120);
            entity.Property(item => item.Subject).HasMaxLength(400).IsRequired();
            entity.Property(item => item.Summary).HasMaxLength(6000).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
            entity.Property(item => item.ResolvedUtc);

            entity.HasIndex(item => item.ReferenceCode).IsUnique();
            entity.HasIndex(item => new { item.CustomerId, item.Status, item.Priority, item.UpdatedUtc });
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(item => item.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TicketMessage>(entity =>
        {
            entity.ToTable("TicketMessages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AuthorType).HasConversion(authorTypeConverter).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Body).HasMaxLength(10000).IsRequired();
            entity.Property(item => item.Source).HasConversion(messageSourceConverter).HasMaxLength(20).IsRequired();
            entity.Property(item => item.ExternalMessageId).HasMaxLength(320);
            entity.Property(item => item.CreatedUtc).IsRequired();

            entity.HasOne(item => item.Ticket)
                .WithMany()
                .HasForeignKey(item => item.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.TicketId, item.CreatedUtc });
            entity.HasIndex(item => item.ExternalMessageId).IsUnique();
        });

        builder.Entity<TicketAttachment>(entity =>
        {
            entity.ToTable("TicketAttachments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FileName).HasMaxLength(400).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(200).IsRequired();
            entity.Property(item => item.StoragePath).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.SizeBytes).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();

            entity.HasOne(item => item.Ticket)
                .WithMany()
                .HasForeignKey(item => item.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.TicketId);
        });

        builder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("AuditEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(120).IsRequired();
            entity.Property(item => item.PayloadJson).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.HasIndex(item => new { item.CustomerId, item.CreatedUtc });
        });

        builder.Entity<OutboundEmailMessage>(entity =>
        {
            entity.ToTable("OutboundEmailMessages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ToAddress).HasMaxLength(320).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Body).IsRequired();
            entity.Property(item => item.CorrelationKey).HasMaxLength(240).IsRequired();
            entity.Property(item => item.Status).HasConversion(emailStatusConverter).HasMaxLength(20).IsRequired();
            entity.Property(item => item.AttemptCount).IsRequired();
            entity.Property(item => item.LastError).HasMaxLength(2000);
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.SentUtc);
            entity.Property(item => item.DeadLetteredUtc);

            entity.HasIndex(item => new { item.Status, item.CreatedUtc });
            entity.HasIndex(item => item.CorrelationKey);
        });

        builder.Entity<AiRun>(entity =>
        {
            entity.ToTable("AiRuns");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Model).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Mode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.PromptHash).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Outcome).HasMaxLength(120).IsRequired();
            entity.Property(item => item.InputTokens).IsRequired();
            entity.Property(item => item.OutputTokens).IsRequired();
            entity.Property(item => item.Confidence).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.HasIndex(item => new { item.TicketId, item.CreatedUtc });

            entity.HasOne<Ticket>()
                .WithMany()
                .HasForeignKey(item => item.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TicketAiSuggestion>(entity =>
        {
            entity.ToTable("TicketAiSuggestions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DraftResponse).HasMaxLength(10000).IsRequired();
            entity.Property(item => item.SuggestedCategory).HasMaxLength(120).IsRequired();
            entity.Property(item => item.SuggestedPriority).HasMaxLength(40).IsRequired();
            entity.Property(item => item.RiskLevel).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Confidence).IsRequired();
            entity.Property(item => item.Status).HasConversion(aiSuggestionStatusConverter).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();

            entity.HasOne(item => item.Ticket)
                .WithMany()
                .HasForeignKey(item => item.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.TicketId, item.CreatedUtc });
        });

        builder.Entity<KnowledgeArticle>(entity =>
        {
            entity.ToTable("KnowledgeArticles");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(300).IsRequired();
            entity.Property(item => item.ContentMarkdown).IsRequired();
            entity.Property(item => item.Status).HasConversion(knowledgeArticleStatusConverter).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Version).IsRequired();
            entity.Property(item => item.AiGenerated).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
            entity.Property(item => item.PublishedUtc);
            entity.Property(item => item.ArchivedUtc);
            entity.HasIndex(item => new { item.CustomerId, item.Status, item.UpdatedUtc });
            entity.HasIndex(item => item.SourceTicketId);

            entity.HasOne<Ticket>()
                .WithMany()
                .HasForeignKey(item => item.SourceTicketId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
