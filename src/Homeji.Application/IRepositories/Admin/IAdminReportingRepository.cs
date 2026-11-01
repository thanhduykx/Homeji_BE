using Homeji.Application.DTOs.Admin;
using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.Admin;

public interface IAdminReportingRepository
{
    Task<AdminPageDto<AdminRegisteredAccountDto>> GetAccountsAsync(string? keyword, int page, int pageSize, CancellationToken cancellationToken);
    Task<AdminPageDto<AdminMemberDto>> GetMembersAsync(string? keyword, string? state, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminRegisteredAccountDto>> GetAllAccountsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminMemberDto>> GetAllMembersAsync(CancellationToken cancellationToken);
    Task<AdminReportExport?> FindRequestAsync(Guid userId, Guid requestId, CancellationToken cancellationToken);
    Task<AdminReportExport> AddAsync(AdminReportExport report, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminReportExportDto>> GetExportsAsync(CancellationToken cancellationToken);
    Task<AdminReportFileDto?> DownloadAsync(Guid id, CancellationToken cancellationToken);
}

public interface IAdminReportWorkbookWriter
{
    byte[] Write(AdminReportingSnapshot snapshot);
}
