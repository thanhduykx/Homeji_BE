using Homeji.Api.Authentication;
using Homeji.Api.Controllers;
using Homeji.Api.ErrorHandling;
using Homeji.Api.RateLimiting;
using Homeji.Api.Realtime;
using Homeji.Api.Middlewares;
using Homeji.Api.BackgroundJobs;
using Homeji.Api.Security;
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

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 128 * 1024;
});

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
if (builder.Configuration.GetValue("BackgroundJobs:Enabled", true))
{
    builder.Services.AddHostedService<MarketplaceOrderExpirationWorker>();
    builder.Services.AddHostedService<PaymentExpirationWorker>();
    // Legacy seed repairs must be explicitly enabled; startup must preserve edits
    // made by users and imports rather than rewriting their titles or locations.
    if (builder.Configuration.GetValue("LegacyData:NormalizeLocationsOnStartup", false))
    {
        builder.Services.AddHostedService<MarketplaceSellerLocationNormalizer>();
    }
}

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

builder.Services.AddTrustedProxyHeaders(builder.Configuration);

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

await app.RunAsync();

public partial class Program;
