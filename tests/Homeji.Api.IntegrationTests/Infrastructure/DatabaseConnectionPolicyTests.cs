using Homeji.Infrastructure;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class DatabaseConnectionPolicyTests
{
    [Theory]
    [InlineData("Host=aws-0-ap-southeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=fixture;Password=fixture", 5)]
    [InlineData("Host=aws-0-ap-southeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=fixture;Password=fixture;Maximum Pool Size=3", 3)]
    [InlineData("Host=127.0.0.1;Database=homeji_quality;Username=fixture", 100)]
    public void Session_pool_leaves_capacity_for_overlapping_deployments(string raw, int expectedMaximum)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = raw }).Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var actual = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        Assert.Equal(expectedMaximum, actual.MaxPoolSize);
        if (actual.Host!.EndsWith(".pooler.supabase.com", StringComparison.Ordinal))
        {
            Assert.Equal(0, actual.MinPoolSize);
            Assert.InRange(actual.ConnectionIdleLifetime, 1, 60);
        }
    }
}
