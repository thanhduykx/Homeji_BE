using Homeji.Domain.Enums;
namespace Homeji.Application.DTOs.Roommates;
public sealed record RoommateProfileDto(RoommateIntent Intent, bool IsDiscoverable, string? Introduction);
public sealed record RoommateDirectoryCandidateDto(Guid UserId, string DisplayName, string? AvatarPath,
    string? School, string? PreferredArea, decimal? MaxBudget, SleepHabit SleepHabit,
    PetPreference PetPreference, SmokingPreference SmokingPreference, RoommateIntent Intent,
    string? Introduction, int? CompatibilityScore);
public sealed record RoommateDirectoryPageDto(IReadOnlyList<RoommateDirectoryCandidateDto> Items, int TotalCount, int Page, int PageSize);
