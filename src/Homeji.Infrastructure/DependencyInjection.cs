using Homeji.Application.IServices.RentalPosts;
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
using Homeji.Application.IRepositories.Admin;
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
        services.AddScoped<Homeji.Application.IRepositories.Roommates.IRoommateDirectoryRepository, RoommateDirectoryRepository>();
        services.AddScoped<IAccountEmailRepository, AccountEmailRepository>();
        services.AddScoped<IRentalPostRepository, RentalPostRepository>();
        services.AddScoped<IRentalSourceListingRepository, RentalSourceListingRepository>();
        services.AddScoped<IFoodSourcePlaceRepository, FoodSourcePlaceRepository>();
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
        services.Configure<GoogleCommuteOptions>(configuration.GetSection("Ai:Commute"));
        services.AddHttpClient<ICommuteClient, GoogleCommuteClient>();
        services.AddScoped<IViewingAppointmentRepository, ViewingAppointmentRepository>();
        services.AddScoped<ILandlordVerificationRepository, LandlordVerificationRepository>();
        services.AddScoped<IUserActivityRepository, UserActivityRepository>();
        services.AddScoped<IPostConversationRepository, PostConversationRepository>();
        services.AddScoped<IRentalWantedPostRepository, RentalWantedPostRepository>();
        services.AddScoped<IMarketplaceOrderRepository, MarketplaceOrderRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IWalletWithdrawalRepository, WalletWithdrawalRepository>();
        services.AddScoped<IAdminAnalyticsRepository, AdminAnalyticsRepository>();
        services.AddScoped<Homeji.Application.IRepositories.Admin.IAdminWorkQueueRepository, AdminWorkQueueRepository>();
        services.AddScoped<IWebsiteTrafficRepository, WebsiteTrafficRepository>();

        services.Configure<SupaBaseAuthOptions>(configuration.GetSection("Supabase"));
        services.AddOptions<EmailDeliveryOptions>()
            .Bind(configuration.GetSection("Email"))
            .Validate(options => options.UsesBrevo || string.Equals(options.Provider, "Smtp", StringComparison.OrdinalIgnoreCase),
                "Email:Provider must be Brevo or Smtp.")
            .Validate(options => !options.RegistrationOtpEnabled || options.UsesBrevo,
                "Registration OTP requires the Brevo HTTPS email provider.")
            .ValidateOnStart();
        var usesBrevo = string.Equals(configuration["Email:Provider"], "Brevo", StringComparison.OrdinalIgnoreCase);
        services.AddOptions<BrevoOptions>()
            .Bind(configuration.GetSection("Email:Brevo"))
            .Validate(options => !usesBrevo || (!string.IsNullOrWhiteSpace(options.ApiKey)
                && !options.ApiKey.Any(char.IsControl)
                && System.Net.Mail.MailAddress.TryCreate(options.FromEmail, out _)
                && options.TimeoutSeconds is >= 5 and <= 120),
                "Brevo requires a valid API key, sender email, and timeout of 5-120 seconds.")
            .ValidateOnStart();
        services.AddOptions<SupaBaseAuthOptions>()
            .Validate(options => !usesBrevo || !string.IsNullOrWhiteSpace(options.ServiceRoleKey),
                "Supabase:ServiceRoleKey is required when Email:Provider is Brevo.")
            .ValidateOnStart();
        services.Configure<SmtpOptions>(configuration.GetSection("Email:Smtp"));
        services.AddOptions<ReceiptEmailOptions>().Bind(configuration.GetSection("Email:Receipts"))
            .Validate(options => !options.Enabled || usesBrevo, "Receipt email delivery requires Brevo.")
            .ValidateOnStart();
        services.AddScoped<ITransactionReceiptService, TransactionReceiptService>();
        services.AddScoped<IPremiumWalletPaymentService, PremiumWalletPaymentService>();
        services.AddScoped<ISearchInterestService, SearchInterestService>();
        services.AddHttpClient<GeminiInterestRanker>();
        services.AddScoped<IOrderReceiptWriter, TransactionReceiptService>();
        services.AddScoped<IReceiptEmailDispatcher, ReceiptEmailDispatcher>();
        services.AddHttpClient<ITransactionalEmailSender, BrevoAccountEmailSender>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
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
        if (usesBrevo)
        {
            services.AddHttpClient<IAccountEmailSender, BrevoAccountEmailSender>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        }
        else
        {
            services.AddScoped<IAccountEmailSender, SmtpAccountEmailSender>();
        }
        services.AddHttpClient<IAccountService, SupabaseAccountService>();
        services.AddHttpClient<ISignupOtpService, SupabaseAccountService>();
        services.AddHttpClient<IPasswordRecoveryOtpService, SupabaseAccountService>();
        services.AddHttpClient<IPaymentService, PaymentService>();
        services.AddHttpClient<IAiSearchTextParser, GeminiSearchTextParser>();
        services.AddHttpClient<IChatbotAiClient, GeminiChatbotClient>();

        services.Configure<CloudinaryOptions>(configuration.GetSection(CloudinaryOptions.SectionName));
        services.AddHttpClient<IImageUploadService, CloudinaryImageUploadService>();
        services.AddScoped<IConversationImageProcessor, ConversationImageProcessor>();

        return services;
    }
}
