using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Homeji.Api.IntegrationTests;

public sealed class HomejiApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Host=localhost;Port=5432;Database=postgres;Username=postgres");
        builder.UseSetting("Supabase:ProjectUrl", "https://test-project.supabase.co");
        builder.UseSetting("Supabase:Audience", "authenticated");
        builder.UseSetting("Api:EnableOpenApi", "false");
        builder.UseSetting("Api:UseHsts", "false");
    }
}
