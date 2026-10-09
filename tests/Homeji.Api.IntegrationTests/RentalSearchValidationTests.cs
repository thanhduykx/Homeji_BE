using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Homeji.Api.IntegrationTests;

public sealed class RentalSearchValidationTests
{
    [Theory]
    [InlineData("minAvailableSlots=0")]
    [InlineData("minAvailableSlots=21")]
    [InlineData("excludedAmenities=")]
    [InlineData("ids=not-a-guid")]
    public async Task Invalid_shortlist_filters_return_400_before_database_access(string query)
    {
        using var factory = new HomejiApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false,
        });
        using var response = await client.GetAsync(new Uri("/api/rental-posts?" + query, UriKind.Relative));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
