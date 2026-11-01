using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class ConversationReadQueryTests
{
    [Fact]
    public void HistoryQueryDoesNotLoadAttachmentBinaryForEveryRefresh()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=query_only;Username=query_only;Password=unused").Options);
        var sql = new PostConversationRepository(db).MessageHistoryQuery(Guid.NewGuid()).ToQueryString();
        Assert.DoesNotContain(".\"Content\"", sql);
        Assert.Contains(".\"MimeType\"", sql);
    }

    [Fact]
    public void UnreadQueryCountsInDatabaseAndRespectsParticipantReadTimes()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=query_only;Username=query_only;Password=unused").Options);
        var sql = new PostConversationRepository(db).UnreadCountsQuery(Guid.NewGuid(), [Guid.NewGuid()]).ToQueryString();
        Assert.Contains("count(*)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("ParticipantALastReadAt", sql);
        Assert.Contains("ParticipantBLastReadAt", sql);
        Assert.DoesNotContain(".\"Body\"", sql);
    }
}
