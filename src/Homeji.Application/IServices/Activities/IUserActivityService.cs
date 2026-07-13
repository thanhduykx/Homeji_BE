using Homeji.Application.DTOs.Activities;

namespace Homeji.Application.IServices.Activities;

public interface IUserActivityService
{
    Task RecordAsync(
        Guid userId,
        string action,
        string resourcePath,
        string httpMethod,
        int responseStatusCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserActivityDto>> GetMineAsync(int take, CancellationToken cancellationToken = default);
}
