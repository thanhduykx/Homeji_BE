using Homeji.Application.DTOs.Marketplace;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Application.IServices.Marketplace;

namespace Homeji.Application.Services.Marketplace;

public sealed class FoodSourcePlaceService(IFoodSourcePlaceRepository repository) : IFoodSourcePlaceService
{
    public Task<IReadOnlyList<FoodSourcePlaceDto>> ListAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(cancellationToken);
}
