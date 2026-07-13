using FluentValidation;
using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.IServices.AI;
using Homeji.Application.IServices.Appointments;
using Homeji.Application.IServices.Verifications;
using Homeji.Application.IServices.Activities;
using Homeji.Application.IServices.Conversations;
using Homeji.Application.IServices.WantedPosts;
using Homeji.Application.IServices.MarketplaceOrders;
using Homeji.Application.IServices.Chatbot;
using Homeji.Application.IServices.Marketplace;
using Homeji.Application.Services.Admin;
using Homeji.Application.Services.AI;
using Homeji.Application.Services.Appointments;
using Homeji.Application.Services.Verifications;
using Homeji.Application.Services.Activities;
using Homeji.Application.Services.Conversations;
using Homeji.Application.Services.WantedPosts;
using Homeji.Application.Services.MarketplaceOrders;
using Homeji.Application.Services.Chatbot;
using Homeji.Application.Services.Marketplace;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Moderation;
using Homeji.Application.Services.Notifications;
using Homeji.Application.IServices.Profiles;
using Homeji.Application.IServices.Admin;
using Homeji.Application.IServices.Notifications;
using Homeji.Application.IServices.RentalPosts;
using Homeji.Application.IServices.Reports;
using Homeji.Application.IServices.Reviews;
using Homeji.Application.IServices.Roommates;
using Homeji.Application.IServices.RoommateChats;
using Homeji.Application.IServices.SavedPosts;
using Homeji.Application.IServices.Subscriptions;
using Homeji.Application.Services.Profiles;
using Homeji.Application.Services.RentalPosts;
using Homeji.Application.Services.Reports;
using Homeji.Application.Services.Reviews;
using Homeji.Application.Services.Roommates;
using Homeji.Application.Services.RoommateChats;
using Homeji.Application.Services.SavedPosts;
using Homeji.Application.Services.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Homeji.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<UserContext>();
        services.AddScoped<ContentModerationService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IRentalPostService, RentalPostService>();
        services.AddScoped<ISavedPostService, SavedPostService>();
        services.AddScoped<IRoommateInvitationService, RoommateInvitationService>();
        services.AddScoped<IRoommateChatService, RoommateChatService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IRentalReviewService, RentalReviewService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.TryAddSingleton<INotificationRealtimePublisher, NoOpNotificationRealtimePublisher>();
        services.AddScoped<IAdminModerationService, AdminModerationService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IAiSearchService, AiSearchService>();
        services.AddScoped<IChatbotService, ChatbotService>();
        services.AddScoped<IMarketplacePostService, MarketplacePostService>();
        services.AddScoped<IViewingAppointmentService, ViewingAppointmentService>();
        services.AddScoped<ILandlordVerificationService, LandlordVerificationService>();
        services.AddScoped<IUserActivityService, UserActivityService>();
        services.AddScoped<IPostConversationService, PostConversationService>();
        services.AddScoped<IRentalWantedPostService, RentalWantedPostService>();
        services.AddScoped<IMarketplaceOrderService, MarketplaceOrderService>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
