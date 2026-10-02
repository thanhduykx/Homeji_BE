using Homeji.Application.DTOs.Admin;

namespace Homeji.Application.IServices.Admin;

public interface IWebsiteTrafficService
{
    Task RecordAsync(RecordWebsitePageViewDto request, CancellationToken cancellationToken);
    Task<WebsiteTrafficReportDto> GetReportAsync(int days, CancellationToken cancellationToken);
}
