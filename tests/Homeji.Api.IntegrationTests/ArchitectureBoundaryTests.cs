using System.Reflection;
using Homeji.Api.Controllers;
using Homeji.Application.IRepositories.Conversations;
using Homeji.Application.IRepositories.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.IntegrationTests;

public sealed class ArchitectureBoundaryTests
{
    // Keep these exceptions scoped to the existing chat actions until their reads move to Application services.
    private static readonly string[] ExistingActionExceptions =
    [
        Describe(typeof(ChatMediaController), nameof(ChatMediaController.SendGif), "repository", typeof(IPostConversationRepository)),
        Describe(typeof(ConversationsController), nameof(ConversationsController.GetMine), "subscriptions", typeof(IUserSubscriptionRepository)),
    ];

    [Fact]
    public void Controllers_DependOnServices_NotRepositoriesOrDbContext()
    {
        var controllerTypes = typeof(ProfileController).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true }
                && typeof(ControllerBase).IsAssignableFrom(type));

        var violations = new List<string>();
        foreach (var controllerType in controllerTypes)
        {
            var constructorParameters = controllerType
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters());
            var actionServices = controllerType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => !method.IsSpecialName && !method.IsDefined(typeof(NonActionAttribute), inherit: true))
                .SelectMany(method => method.GetParameters())
                .Where(parameter => parameter.IsDefined(typeof(FromServicesAttribute)));
            var dependencies = constructorParameters.Concat(actionServices).ToArray();

            violations.AddRange(dependencies
                .Where(parameter => IsForbiddenDependency(parameter.ParameterType))
                .Select(parameter => Describe(controllerType, parameter.Member.Name, parameter.Name, parameter.ParameterType)));

            Assert.Contains(
                dependencies,
                parameter => parameter.ParameterType.Namespace is { } ns
                    && (ns.StartsWith("Homeji.Application.IServices", StringComparison.Ordinal)
                        || ns.StartsWith("Homeji.Application.Abstractions", StringComparison.Ordinal)));
        }

        Assert.Equal(ExistingActionExceptions, violations.Order(StringComparer.Ordinal));
    }

    private static string Describe(Type controller, string member, string? parameter, Type dependency) =>
        $"{controller.Name}.{member}({parameter}: {dependency.FullName})";

    private static bool IsForbiddenDependency(Type type) =>
        type.Name.Contains("Repository", StringComparison.Ordinal)
        || type.Name.Contains("DbContext", StringComparison.Ordinal)
        || type.Assembly.GetName().Name == "Homeji.Infrastructure"
        || (type.GetElementType() is { } element && IsForbiddenDependency(element))
        || type.GenericTypeArguments.Any(IsForbiddenDependency);
}
