using Homeji.Application.DTOs.Admin;

namespace Homeji.Application.IServices.Admin;

public interface IAdminAnalyticsService
{
    Task<AdminProductAnalyticsDto> GetProductAnalyticsAsync(
        int periodDays,
        CancellationToken cancellationToken = default);
}
