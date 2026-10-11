using System.Net;
using System.Net.Http.Json;
using Homeji.Application.DTOs.Marketplace;
using Homeji.Application.IRepositories.Marketplace;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Homeji.Api.IntegrationTests;

public sealed class FoodSourcePlacesTests
{
    [Fact]
    public async Task Anonymous_directory_preserves_source_and_uncertain_address_without_seller_account()
    {
        using var baseFactory = new HomejiApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IFoodSourcePlaceRepository>();
            services.AddSingleton<IFoodSourcePlaceRepository, DirectoryRepository>();
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync(new Uri("/api/food-source-places", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var records = await response.Content.ReadFromJsonAsync<FoodSourcePlaceDto[]>();
        var item = Assert.Single(records!);
        Assert.Equal("1 Đường số 1 (chưa có phường)", item.Address);
        Assert.Equal("https://www.nhatot.com/kinh-nghiem/dia-diem-an-vat-quan-9.html", item.SourceUrl);
        Assert.Equal("Chưa xác nhận hoạt động; Maps chỉ là tìm kiếm.", item.VerificationNote);
    }

    private sealed class DirectoryRepository : IFoodSourcePlaceRepository
    {
        public Task<IReadOnlyList<FoodSourcePlaceDto>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FoodSourcePlaceDto>>([new(Guid.NewGuid(), "Chóp Chép",
                "1 Đường số 1 (chưa có phường)", "https://www.nhatot.com/kinh-nghiem/dia-diem-an-vat-quan-9.html",
                "https://www.google.com/maps/search/?api=1&query=Chop+Chep+Quan+9",
                "Chưa xác nhận hoạt động; Maps chỉ là tìm kiếm.", DateTimeOffset.UtcNow)]);
    }
}
