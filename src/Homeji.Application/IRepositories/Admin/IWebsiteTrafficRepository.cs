using Homeji.Application.DTOs.Admin;
using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.Admin;

public interface IWebsiteTrafficRepository
{
    Task RecordAsync(WebsitePageView pageView, CancellationToken cancellationToken);
    Task<WebsiteTrafficReportDto> GetReportAsync(DateTimeOffset from, DateTimeOffset until, int days, CancellationToken cancellationToken);
}
