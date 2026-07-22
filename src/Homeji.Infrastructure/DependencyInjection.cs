using Homeji.Application.IRepositories.Moderation;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Application.IRepositories.Wallets;
using Homeji.Application.IRepositories.Accounts;
using Homeji.Application.IRepositories.Chatbot;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.Payments;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Reports;
using Homeji.Application.IRepositories.Reviews;
using Homeji.Application.IRepositories.Roommates;
using Homeji.Application.IRepositories.RoommateChats;
using Homeji.Application.IRepositories.SavedPosts;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IRepositories.Appointments;
using Homeji.Application.IRepositories.Verifications;
using Homeji.Application.IRepositories.Activities;
using Homeji.Application.IRepositories.Conversations;
using Homeji.Application.IRepositories.WantedPosts;
using Homeji.Application.IRepositories.MarketplaceOrders;
using Homeji.Application.IServices.Accounts;
using Homeji.Application.IServices.AI;
using Homeji.Application.IServices.Chatbot;
using Homeji.Application.IServices.Emails;
using Homeji.Application.IServices.Payments;
using Homeji.Application.IServices.Upload;
using Homeji.Application.Services.AI;
using Homeji.Application.Services.Chatbot;
using Homeji.Application.Services.Subscriptions;
using Homeji.Application.Services.Marketplace;
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
        services.AddScoped<IAccountEmailRepository, AccountEmailRepository>();
        services.AddScoped<IRentalPostRepository, RentalPostRepository>();
        services.AddScoped<ISavedPostRepository, SavedPostRepository>();
        services.AddScoped<IRoommateInvitationRepository, RoommateInvitationRepository>();
        services.AddScoped<IRoommateConversationRepository, RoommateConversationRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IRentalReviewRepository, RentalReviewRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IBadWordRepository, BadWordRepository>();
        services.AddScoped<IMarketplacePostRepository, MarketplacePostRepository>();
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<IUserSubscriptionRepository, UserSubscriptionRepository>();
        services.AddScoped<IChatConversationRepository, ChatConversationRepository>();
        services.AddScoped<IViewingAppointmentRepository, ViewingAppointmentRepository>();
        services.AddScoped<ILandlordVerificationRepository, LandlordVerificationRepository>();
        services.AddScoped<IUserActivityRepository, UserActivityRepository>();
        services.AddScoped<IPostConversationRepository, PostConversationRepository>();
        services.AddScoped<IRentalWantedPostRepository, RentalWantedPostRepository>();
        services.AddScoped<IMarketplaceOrderRepository, MarketplaceOrderRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IWalletWithdrawalRepository, WalletWithdrawalRepository>();
        services.AddScoped<IMarketplaceSellerSubscriptionRepository, MarketplaceSellerSubscriptionRepository>();

        services.Configure<SupaBaseAuthOptions>(configuration.GetSection("Supabase"));
        services.Configure<SmtpOptions>(configuration.GetSection("Email:Smtp"));
        services.Configure<MomoOptions>(configuration.GetSection("Payments:MoMo"));
        services.Configure<PayOsOptions>(configuration.GetSection("Payments:PayOS"));
        services.Configure<PremiumSubscriptionOptions>(configuration.GetSection(PremiumSubscriptionOptions.SectionName));
        services.AddOptions<MarketplaceFinanceOptions>()
            .Bind(configuration.GetSection(MarketplaceFinanceOptions.SectionName))
            .Validate(options => options.IsValid(), "MarketplaceFinance settings are invalid.")
            .ValidateOnStart();
        services.Configure<AiSearchOptions>(configuration.GetSection(AiSearchOptions.SectionName));
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.Configure<ChatbotOptions>(configuration.GetSection(ChatbotOptions.SectionName));
        services.AddScoped<IAccountEmailSender, SmtpAccountEmailSender>();
        services.AddHttpClient<IAccountService, SupabaseAccountService>();
        services.AddHttpClient<IPaymentService, PaymentService>();
        services.AddHttpClient<IAiSearchTextParser, GeminiSearchTextParser>();
        services.AddHttpClient<IChatbotAiClient, GeminiChatbotClient>();

        services.Configure<CloudinaryOptions>(configuration.GetSection(CloudinaryOptions.SectionName));
        services.AddHttpClient<IImageUploadService, CloudinaryImageUploadService>();
        services.AddScoped<IConversationImageProcessor, ConversationImageProcessor>();

        return services;
    }
}
