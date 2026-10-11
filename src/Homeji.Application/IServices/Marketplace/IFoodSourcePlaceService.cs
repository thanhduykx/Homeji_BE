using Homeji.Application.DTOs.Marketplace;

namespace Homeji.Application.IServices.Marketplace;

public interface IFoodSourcePlaceService
{
    Task<IReadOnlyList<FoodSourcePlaceDto>> ListAsync(CancellationToken cancellationToken);
}
