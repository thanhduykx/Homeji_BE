using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Activities;

public sealed record UserActivityDto(
    Guid Id,
    string Action,
    string ResourcePath,
    string HttpMethod,
    int ResponseStatusCode,
    UserActivityType Type,
    Guid? RelatedEntityId,
    string? Details,
    DateTimeOffset OccurredAt);
