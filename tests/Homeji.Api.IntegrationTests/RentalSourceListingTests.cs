using System.Net;
using System.Net.Http.Json;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Homeji.Api.IntegrationTests;

public sealed class RentalSourceListingTests
{
    [Theory]
    [InlineData("district=ha-noi")]
    [InlineData("page=0")]
    [InlineData("pageSize=51")]
    [InlineData("minPrice=3000000&maxPrice=2000000")]
    public async Task Invalid_filters_return_400_before_database_access(string query)
    {
        using var factory = new HomejiApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync(new Uri($"/api/rental-source-listings?{query}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task External_images_roundtrip_and_literal_search_does_not_expand_wildcards()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality")
            throw new InvalidOperationException("Only the disposable loopback database is permitted.");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var token = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var matching = Listing($"{token} 50%", now);
        var ordinary = Listing($"{token} 50", now.AddSeconds(-1));
        db.RentalSourceListings.AddRange(matching, ordinary);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var repository = new RentalSourceListingRepository(db);
        var result = await repository.SearchAsync(new RentalSourceSearchDto(token + " 50%", "quan-9", null, null), CancellationToken.None);
        var item = Assert.Single(result);
        Assert.Equal(matching.Id, item.Id);
        Assert.Equal(matching.ImageUrls, item.ImageUrls);
        Assert.Empty(db.ChangeTracker.Entries());
        using var factory = new HomejiApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connection);
            builder.UseSetting("BackgroundJobs:Enabled", "false");
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetFromJsonAsync<RentalSourceListingDto>(new Uri($"/api/rental-source-listings/{matching.Id}", UriKind.Relative));
        Assert.Equal(matching.SourceUrl, response!.SourceUrl);
        Assert.Equal(matching.ImageUrls, response.ImageUrls);
        // A raw import must also obey the database's geography constraint.
        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE homeji.rental_source_listings SET district = 'ha-noi' WHERE id = {matching.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    private static RentalSourceListing Listing(string title, DateTimeOffset now)
    {
        var sourceId = Random.Shared.Next(100_000_000, int.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return RentalSourceListing.Create(sourceId, $"https://phongtro123.com/test-pr{sourceId}.html",
            title, "Thủ Đức", "quan-9", 2_000_000, 20,
            ["https://pt123.cdn.static123.com/test.jpg"], now, now);
    }
}
