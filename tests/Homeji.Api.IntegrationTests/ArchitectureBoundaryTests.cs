using System.Reflection;
using Homeji.Api.Controllers;
using Homeji.Application.IServices.Profiles;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.IntegrationTests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void Controllers_DependOnServices_NotRepositoriesOrDbContext()
    {
        var controllerTypes = typeof(ProfileController).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true }
                && typeof(ControllerBase).IsAssignableFrom(type));

        foreach (var controllerType in controllerTypes)
        {
            var constructorParameters = controllerType
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .ToArray();

            Assert.DoesNotContain(
                constructorParameters,
                parameterType => parameterType.Name.Contains("Repository", StringComparison.Ordinal)
                    || parameterType.Name.Contains("DbContext", StringComparison.Ordinal)
                    || parameterType.Namespace?.Contains("Infrastructure", StringComparison.Ordinal) == true);

            Assert.Contains(
                constructorParameters,
                parameterType => parameterType == typeof(IUserProfileService)
                    || parameterType.Namespace?.StartsWith("Homeji.Application.IServices", StringComparison.Ordinal) == true);
        }
    }
}
