using Homeji.Application.DTOs.AI;

namespace Homeji.Application.IServices.AI;

public interface ICommuteClient
{
    Task<IReadOnlyCollection<CommuteEstimateDto>> ComputeAsync(IReadOnlyCollection<CommuteOriginDto> origins,
        CommuteDestinationDto destination, CancellationToken cancellationToken = default);
}
