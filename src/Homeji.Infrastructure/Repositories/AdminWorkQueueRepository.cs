using Homeji.Application.IRepositories.Admin;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class AdminWorkQueueRepository(ApplicationDbContext dbContext) : IAdminWorkQueueRepository
{
    public async Task<AdminWorkQueueSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var posts = dbContext.RentalPosts.AsNoTracking().Where(post => post.Status == RentalPostStatus.Pending);
        var reports = dbContext.Reports.AsNoTracking().Where(report => report.Status == ReportStatus.New);
        // Keep queries sequential: this repository uses one scoped DbContext.
        var postCount = await posts.CountAsync(cancellationToken);
        var reportCount = await reports.CountAsync(cancellationToken);
        var postTasks = await BuildPostTasksQuery().ToListAsync(cancellationToken);
        var reportTasks = await BuildReportTasksQuery().ToListAsync(cancellationToken);
        return new(postCount, reportCount, postTasks, reportTasks);
    }

    internal IQueryable<AdminPendingPost> BuildPostTasksQuery() => dbContext.RentalPosts.AsNoTracking()
        .Where(post => post.Status == RentalPostStatus.Pending)
        .OrderBy(post => post.CreatedAt).ThenBy(post => post.Id)
        .Select(post => new AdminPendingPost(post.Id, post.Title)).Take(10);

    internal IQueryable<AdminPendingReport> BuildReportTasksQuery() => dbContext.Reports.AsNoTracking()
        .Where(report => report.Status == ReportStatus.New)
        .OrderBy(report => report.CreatedAt).ThenBy(report => report.Id)
        .Select(report => new AdminPendingReport(report.Id, report.TargetType, report.Reason)).Take(10);
}
