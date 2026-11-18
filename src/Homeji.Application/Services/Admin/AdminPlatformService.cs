using Homeji.Application.DTOs.Admin;
using Homeji.Application.IServices.Admin;
using Homeji.Application.Services.Common;

namespace Homeji.Application.Services.Admin;

public interface IAdminPlatformReader
{
    Task<AdminPlatformSnapshot> ReadAsync(CancellationToken cancellationToken);
}

public sealed class AdminPlatformService(UserContext userContext, IAdminPlatformReader reader) : IAdminPlatformService
{
    public async Task<AdminPlatformSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        UserContext.EnsureAdmin(await userContext.GetRequiredProfileAsync(cancellationToken));
        return await reader.ReadAsync(cancellationToken);
    }
}
