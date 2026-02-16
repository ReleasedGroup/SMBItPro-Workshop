using System.Text;
using System.Text.Json;
using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Api.Observability;
using Helpdesk.Light.Api.Auth;
using Helpdesk.Light.Api.Tenancy;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Infrastructure;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Health;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
JwtOptions jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContextAccessor, HttpTenantContextAccessor>();
builder.Services.AddScoped<IJwtTokenIssuer, JwtTokenIssuer>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TechnicianOrAdmin", policy =>
        policy.RequireRole(RoleNames.Technician, RoleNames.MspAdmin));

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

string[] frontendAllowedOrigins = builder.Configuration
    .GetSection("Cors:Frontend:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

if (frontendAllowedOrigins.Length == 0)
{
    frontendAllowedOrigins = new[] { "http://localhost:5006", "https://localhost:7262" };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(frontendAllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services
    .AddHealthChecks()
    .AddCheck<SqliteHealthCheck>("sqlite", tags: ["ready"])
    .AddCheck<MailAdapterHealthCheck>("mail_adapter", tags: ["ready"])
    .AddCheck<AiProviderHealthCheck>("ai_provider", tags: ["ready"]);

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Frontend");
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = WriteHealthResponse
}).AllowAnonymous();

await SeedData.EnsureSeededAsync(app.Services);

app.Run();

static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        entries = report.Entries.ToDictionary(
            item => item.Key,
            item => new
            {
                status = item.Value.Status.ToString(),
                description = item.Value.Description,
                durationMs = item.Value.Duration.TotalMilliseconds
            })
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}

public partial class Program;
