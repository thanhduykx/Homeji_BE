using Homeji.Application.DTOs.Admin;

namespace Homeji.Application.IServices.Admin;

public interface IAdminReportingService
{
    Task<AdminPageDto<AdminRegisteredAccountDto>> GetAccountsAsync(string? keyword, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminPageDto<AdminMemberDto>> GetMembersAsync(string? keyword, string? state, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminReportExportDto> CreateAsync(CreateAdminReportDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminReportExportDto>> GetExportsAsync(CancellationToken cancellationToken = default);
    Task<AdminReportFileDto> DownloadAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IAdminReportEmailDispatcher
{
    Task DispatchBatchAsync(CancellationToken cancellationToken);
}
