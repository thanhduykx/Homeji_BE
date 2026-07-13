namespace Homeji.Application.DTOs.Activities;

public sealed record UserActivityDto(
    Guid Id,
    string Action,
    string ResourcePath,
    string HttpMethod,
    int ResponseStatusCode,
    DateTimeOffset OccurredAt);
