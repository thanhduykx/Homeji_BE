using Homeji.Application.DTOs.Marketplace;

namespace Homeji.Application.IRepositories.Marketplace;

public interface IFoodSourcePlaceRepository
{
    Task<IReadOnlyList<FoodSourcePlaceDto>> ListAsync(CancellationToken cancellationToken);
}
