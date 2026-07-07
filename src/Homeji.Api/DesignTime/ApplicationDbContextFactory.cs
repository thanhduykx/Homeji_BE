using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Homeji.Api.DesignTime;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var apiProjectPath = ResolveApiProjectPath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is required in src/Homeji.Api/appsettings.json.");
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static string ResolveApiProjectPath()
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (currentDirectory is not null)
        {
            var rootRelativeApiPath = Path.Combine(currentDirectory.FullName, "src", "Homeji.Api");
            if (File.Exists(Path.Combine(rootRelativeApiPath, "appsettings.json")))
            {
                return rootRelativeApiPath;
            }

            if (File.Exists(Path.Combine(currentDirectory.FullName, "Homeji.Api.csproj"))
                && File.Exists(Path.Combine(currentDirectory.FullName, "appsettings.json")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException("Could not locate src/Homeji.Api/appsettings.json for EF Core tooling.");
    }
}
