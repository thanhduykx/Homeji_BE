using Homeji.Api.Authentication;
using Homeji.Api.Controllers;
using Homeji.Api.ErrorHandling;
using Homeji.Api.RateLimiting;
using Homeji.Api.Realtime;
using Homeji.Api.Middlewares;
using Homeji.Api.BackgroundJobs;
using Homeji.Application;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Abstractions.Presence;
using Homeji.Infrastructure;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Local clones use only tracked appsettings files. Production retains environment
// variables so managed hosts can inject secrets without committing them to Git.
if (builder.Environment.IsDevelopment())
{
    RemoveEnvironmentBackedConfigurationSources(builder.Configuration);
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("api", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Homeji API",
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOptions<PaymentRedirectOptions>()
    .BindConfiguration(PaymentRedirectOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<MarketplaceOrderExpirationWorker>();
builder.Services.AddHostedService<MarketplaceSellerLocationNormalizer>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSupabaseAuthentication(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
builder.Services.AddSingleton<IOnlineUserTracker, OnlineUserTracker>();
builder.Services.AddSingleton<IUserSessionRevocationCache, MemoryUserSessionRevocationCache>();
builder.Services.AddSingleton<IUserSessionRealtimePublisher, SignalRUserSessionPublisher>();
builder.Services.AddSingleton<INotificationRealtimePublisher, SignalRNotificationPublisher>();
builder.Services.AddHomejiRateLimiting(builder.Configuration);

// Render / reverse proxies forward the real client IP via X-Forwarded-For.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(origin => origin.Value)
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Cast<string>()
    .ToArray();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    }));

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

if (builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", builder.Environment.IsProduction()))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var database = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await database.Database.MigrateAsync();
}

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (builder.Configuration.GetValue("Api:EnableOpenApi", false))
{
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "swagger/{documentName}/swagger.json";
    });

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/api/swagger.json", "Homeji API");
        options.RoutePrefix = "swagger";
    });

}

if (builder.Configuration.GetValue("Api:UseHsts", true))
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<UserActivityMiddleware>();
app.UseRateLimiter();

app.MapHealthChecks(
        "/health/live",
        new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();

app.MapHealthChecks(
        "/health/ready",
        new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous();

app.MapGet(
        "/",
        () => Results.Redirect("/swagger"))
    .AllowAnonymous();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();

static void RemoveEnvironmentBackedConfigurationSources(ConfigurationManager configuration)
{
    for (var index = configuration.Sources.Count - 1; index >= 0; index--)
    {
        var sourceName = configuration.Sources[index].GetType().Name;
        if (sourceName is "EnvironmentVariablesConfigurationSource" or "UserSecretsConfigurationSource")
        {
            configuration.Sources.RemoveAt(index);
        }
    }
}

public partial class Program;
