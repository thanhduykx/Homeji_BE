using Homeji.Application.DTOs.Admin;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.DTOs.Reports;
using Homeji.Domain.Enums;

namespace Homeji.Application.IServices.Admin;

public interface IAdminModerationService
{
    Task<IReadOnlyList<AdminActiveUserDto>> GetActiveUsersAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RentalPostSummaryDto>> GetPendingRentalPostsAsync(CancellationToken cancellationToken = default);

    Task<RentalPostDto> ApproveRentalPostAsync(
        Guid postId,
        ApproveRentalPostDto request,
        CancellationToken cancellationToken = default);

    Task<RentalPostDto> RejectRentalPostAsync(Guid postId, RejectRentalPostDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportDto>> GetReportsAsync(ReportStatus? status, CancellationToken cancellationToken = default);

    Task<ReportDto> ResolveReportAsync(Guid reportId, ResolveReportDto request, CancellationToken cancellationToken = default);

    Task<ReportDto> RejectReportAsync(Guid reportId, ResolveReportDto request, CancellationToken cancellationToken = default);
}
