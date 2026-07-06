using FluentValidation;
using Homeji.Application.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Homeji.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
