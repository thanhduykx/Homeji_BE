using Homeji.Application.DTOs.Admin;

namespace Homeji.Application.IServices.Admin;

public interface IAdminPlatformService
{
    Task<AdminPlatformSnapshot> GetAsync(CancellationToken cancellationToken);
}
