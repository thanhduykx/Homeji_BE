using Homeji.Application.DTOs.Roommates;
using Homeji.Domain.Enums;
namespace Homeji.Application.IServices.Roommates;
public interface IRoommateDirectoryService
{
    Task<RoommateProfileDto> GetMineAsync(CancellationToken cancellationToken = default);
    Task<RoommateProfileDto> UpdateMineAsync(RoommateProfileDto request, CancellationToken cancellationToken = default);
    Task<RoommateDirectoryPageDto> SearchAsync(RoommateIntent? intent, string? keyword, int page, int pageSize, CancellationToken cancellationToken = default);
}
