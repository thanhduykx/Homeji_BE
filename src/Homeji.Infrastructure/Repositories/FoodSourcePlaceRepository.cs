using Homeji.Application.DTOs.Marketplace;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class FoodSourcePlaceRepository(ApplicationDbContext context) : IFoodSourcePlaceRepository
{
    public async Task<IReadOnlyList<FoodSourcePlaceDto>> ListAsync(CancellationToken cancellationToken) =>
        await context.FoodSourcePlaces.AsNoTracking().OrderBy(place => place.Name).Take(50)
            .Select(place => new FoodSourcePlaceDto(place.Id, place.Name, place.Address, place.SourceUrl,
                place.MapsSearchUrl, place.VerificationNote, place.CollectedAt) { Latitude = place.Latitude, Longitude = place.Longitude })
            .ToArrayAsync(cancellationToken);
}
