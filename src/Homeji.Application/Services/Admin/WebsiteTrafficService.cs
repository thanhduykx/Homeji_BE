using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.IRepositories.Admin;
using Homeji.Application.IServices.Admin;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.Admin;

public sealed class WebsiteTrafficService(UserContext userContext, IWebsiteTrafficRepository repository, TimeProvider clock) : IWebsiteTrafficService
{
    // Only coarse page categories are accepted; never URLs, search terms or user identifiers.
    public static readonly IReadOnlySet<string> Pages = new HashSet<string>(StringComparer.Ordinal)
    {
        "home", "rental-detail", "explore", "marketplace", "wanted", "saved", "profile",
        "notifications", "invitations", "appointments", "messages", "payments", "my-posts",
        "create-post", "edit-post", "login", "register", "privacy", "terms", "other",
    };

    public Task RecordAsync(RecordWebsitePageViewDto request, CancellationToken cancellationToken)
    {
        var page = request.Page ?? "";
        if (request.EventId == Guid.Empty || request.SessionId == Guid.Empty || !Pages.Contains(page))
            throw new RequestValidationException(new Dictionary<string, string[]> { ["pageView"] = ["Thông tin lượt ghé không hợp lệ."] });
        return repository.RecordAsync(new WebsitePageView(request.EventId, request.SessionId, page, clock.GetUtcNow()), cancellationToken);
    }

    public async Task<WebsiteTrafficReportDto> GetReportAsync(int days, CancellationToken cancellationToken)
    {
        UserContext.EnsureAdmin(await userContext.GetRequiredProfileAsync(cancellationToken));
        if (days is < 7 or > 90)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["days"] = ["Chọn khoảng từ 7 đến 90 ngày."] });
        var now = clock.GetUtcNow();
        var local = now.ToOffset(TimeSpan.FromHours(7));
        var from = new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, local.Offset).AddDays(1 - days);
        return await repository.GetReportAsync(from.ToUniversalTime(), now, days, cancellationToken);
    }
}
