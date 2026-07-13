using Homeji.Application.DTOs.Activities;
using Homeji.Domain.Enums;

namespace Homeji.Application.IServices.Activities;

public interface IUserActivityService
{
    Task RecordAsync(
        Guid userId,
        string action,
        string resourcePath,
        string httpMethod,
        int responseStatusCode,
        UserActivityType type = UserActivityType.General,
        Guid? relatedEntityId = null,
        string? details = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserActivityDto>> GetMineAsync(UserActivityType? type, int take, CancellationToken cancellationToken = default);
}
