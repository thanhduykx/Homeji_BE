using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Reports;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Reports;
using Homeji.Application.IServices.Reports;
using Homeji.Application.Mappers.Reports;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Reports;

public sealed class ReportService : IReportService
{
    private readonly UserContext _userContext;
    private readonly IReportRepository _reports;
    private readonly IRentalPostRepository _posts;
    private readonly TimeProvider _timeProvider;

    public ReportService(
        UserContext userContext,
        IReportRepository reports,
        IRentalPostRepository posts,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _reports = reports;
        _posts = posts;
        _timeProvider = timeProvider;
    }

    public async Task<ReportDto> CreateAsync(CreateReportDto request, CancellationToken cancellationToken = default)
    {
        var reporterId = _userContext.GetRequiredUserId();
        if (request.TargetType == ReportTargetType.RentalPost
            && await _posts.GetByIdAsync(request.TargetId, cancellationToken) is null)
        {
            throw new NotFoundException(nameof(RentalPost), request.TargetId);
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["reason"] = ["Reason is required."],
            });
        }

        var report = new Report(
            reporterId,
            request.TargetType,
            request.TargetId,
            request.Reason!,
            request.Description,
            _timeProvider.GetUtcNow());
        await _reports.AddAsync(report, cancellationToken);
        await _reports.SaveChangesAsync(cancellationToken);
        return ReportMapper.ToDto(report);
    }
}
