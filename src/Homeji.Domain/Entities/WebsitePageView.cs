namespace Homeji.Domain.Entities;

public sealed class WebsitePageView
{
    private WebsitePageView() { Page = null!; }

    public WebsitePageView(Guid id, Guid sessionId, string page, DateTimeOffset occurredAt)
    {
        Id = id;
        SessionId = sessionId;
        Page = page;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string Page { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
