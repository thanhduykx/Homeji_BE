using System.Runtime.CompilerServices;
using Homeji.Infrastructure.Health;

namespace Homeji.Api.IntegrationTests;

internal static class TestHostMemoryBudget
{
    // One xUnit process hosts every API factory, so its working set says nothing about a production container.
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Raise() => MediaMemoryPressure.ThresholdBytes = 16L * 1024 * 1024 * 1024;
#pragma warning restore CA2255
}
