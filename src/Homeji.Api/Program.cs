using Homeji.Api.Authentication;
using Homeji.Api.ErrorHandling;
using Homeji.Application;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Infrastructure;
using Homeji.Infrastructure.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

RemoveEnvironmentBackedConfigurationSources(builder.Configuration);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSupabaseAuthentication(builder.Configuration);

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

app.UseExceptionHandler();

if (builder.Configuration.GetValue("Api:EnableOpenApi", false))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Homeji API v1");
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
