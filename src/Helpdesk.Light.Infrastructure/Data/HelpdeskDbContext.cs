using Helpdesk.Light.Domain.Entities;
using Helpdesk.Light.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.Infrastructure.Data;

public sealed class HelpdeskDbContext(DbContextOptions<HelpdeskDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerDomain> CustomerDomains => Set<CustomerDomain>();

    public DbSet<UnmappedInboundItem> UnmappedInboundItems => Set<UnmappedInboundItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.IsActive).IsRequired();
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
    }
}
