using FluentValidation;
using Homeji.Application.IServices.AI;
using Homeji.Application.Services.Admin;
using Homeji.Application.Services.AI;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Moderation;
using Homeji.Application.Services.Notifications;
using Homeji.Application.IServices.Profiles;
using Homeji.Application.IServices.Admin;
using Homeji.Application.IServices.Notifications;
using Homeji.Application.IServices.RentalPosts;
using Homeji.Application.IServices.Reports;
using Homeji.Application.IServices.Roommates;
using Homeji.Application.IServices.SavedPosts;
using Homeji.Application.IServices.Subscriptions;
using Homeji.Application.Services.Profiles;
using Homeji.Application.Services.RentalPosts;
using Homeji.Application.Services.Reports;
using Homeji.Application.Services.Roommates;
using Homeji.Application.Services.SavedPosts;
using Homeji.Application.Services.Subscriptions;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAdminModerationService, AdminModerationService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IAiSearchService, AiSearchService>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
