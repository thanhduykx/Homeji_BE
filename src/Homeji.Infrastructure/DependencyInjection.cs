using Homeji.Application.IRepositories.Moderation;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.Payments;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Reports;
using Homeji.Application.IRepositories.Roommates;
using Homeji.Application.IRepositories.SavedPosts;
using Homeji.Application.IServices.Accounts;
using Homeji.Application.IServices.Payments;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Homeji.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is required. Configure it in src/Homeji.Api/appsettings.json.");
        }

        try
        {
            _ = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is invalid.", exception);
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                }));

        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IRentalPostRepository, RentalPostRepository>();
        services.AddScoped<ISavedPostRepository, SavedPostRepository>();
        services.AddScoped<IRoommateInvitationRepository, RoommateInvitationRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IBadWordRepository, BadWordRepository>();
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();

        services.Configure<SupaBaseAuthOptions>(configuration.GetSection("Supabase"));
        services.Configure<MomoOptions>(configuration.GetSection("Payments:MoMo"));
        services.Configure<PayOsOptions>(configuration.GetSection("Payments:PayOS"));
        services.AddHttpClient<IAccountService, SupabaseAccountService>();
        services.AddHttpClient<IPaymentService, PaymentService>();

        return services;
    }
}
