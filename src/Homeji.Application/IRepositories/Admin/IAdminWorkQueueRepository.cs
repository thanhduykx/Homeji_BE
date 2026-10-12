using Homeji.Domain.Enums;

namespace Homeji.Application.IRepositories.Admin;

public sealed record AdminPendingPost(Guid Id, string Title);
public sealed record AdminPendingReport(Guid Id, ReportTargetType TargetType, string Reason);
public sealed record AdminWorkQueueSnapshot(int PendingPosts, int PendingReports,
    IReadOnlyList<AdminPendingPost> Posts, IReadOnlyList<AdminPendingReport> Reports);

public interface IAdminWorkQueueRepository
{
    Task<AdminWorkQueueSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}
