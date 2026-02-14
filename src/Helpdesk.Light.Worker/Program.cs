using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Infrastructure;
using Helpdesk.Light.Worker;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<ITenantContextAccessor, WorkerTenantContextAccessor>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
