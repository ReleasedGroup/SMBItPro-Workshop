using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Abstractions.Ai;
using Helpdesk.Light.Application.Abstractions.Email;
using Helpdesk.Light.Application.Abstractions.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Identity;
using Helpdesk.Light.Infrastructure.Options;
using Helpdesk.Light.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Light.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Helpdesk")
            ?? "Data Source=helpdesk-light.db";

        services.AddDbContext<HelpdeskDbContext>(options => options.UseSqlite(connectionString));
        services.AddHttpClient();

        services.Configure<AttachmentOptions>(configuration.GetSection(AttachmentOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 10;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<HelpdeskDbContext>();

        services.AddSingleton<IRuntimeMetricsRecorder, RuntimeMetricsRecorder>();
        services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();
        services.AddScoped<ICustomerAdministrationService, CustomerAdministrationService>();
        services.AddScoped<IResolverAdministrationService, ResolverAdministrationService>();
        services.AddScoped<ITicketCategoryAdministrationService, TicketCategoryAdministrationService>();
        services.AddScoped<ITenantResolutionService, TenantResolutionService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ITicketAccessGuard, TicketAccessGuard>();
        services.AddScoped<IAttachmentStorage, LocalAttachmentStorage>();
        services.AddScoped<IInboundEmailService, InboundEmailService>();
        services.AddScoped<IOutboundEmailService, OutboundEmailService>();
        services.AddScoped<IEmailTransport, ConfigurableEmailTransport>();
        services.AddScoped<IAiTicketAgentService, AiTicketAgentService>();
        services.AddScoped<ICustomerAiPolicyService, CustomerAiPolicyService>();
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();

        return services;
    }
}
