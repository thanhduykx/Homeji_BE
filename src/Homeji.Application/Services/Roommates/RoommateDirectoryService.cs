using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Roommates;
using Homeji.Application.IRepositories.Roommates;
using Homeji.Application.IServices.Roommates;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
namespace Homeji.Application.Services.Roommates;
public sealed class RoommateDirectoryService(UserContext context, IRoommateDirectoryRepository directory, TimeProvider clock) : IRoommateDirectoryService
{
    private async Task<UserProfile> RenterAsync(CancellationToken ct)
    {
        var user = await context.GetRequiredProfileAsync(ct); UserContext.EnsureRenter(user); return user;
    }
    public async Task<RoommateProfileDto> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var user = await RenterAsync(cancellationToken);
        var profile = await directory.GetAsync(user.Id, cancellationToken);
        return profile is null ? new(RoommateIntent.SeekingAccommodation, false, null) : new(profile.Intent, profile.IsDiscoverable, profile.Introduction);
    }
    public async Task<RoommateProfileDto> UpdateMineAsync(RoommateProfileDto request, CancellationToken cancellationToken = default)
    {
        var user = await RenterAsync(cancellationToken);
        var profile = await directory.GetAsync(user.Id, cancellationToken) ?? new RoommateProfile(user.Id);
        profile.Update(request.Intent, request.IsDiscoverable, request.Introduction, clock.GetUtcNow());
        await directory.SaveAsync(profile, cancellationToken);
        return new(profile.Intent, profile.IsDiscoverable, profile.Introduction);
    }
    public async Task<RoommateDirectoryPageDto> SearchAsync(RoommateIntent? intent, string? keyword, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var user = await RenterAsync(cancellationToken);
        if ((intent.HasValue && !Enum.IsDefined(intent.Value)) || page < 1 || page > 10000 || pageSize < 1 || pageSize > 50 || keyword?.Length > 200)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["query"] = ["Bộ lọc tìm bạn không hợp lệ."] });
        var (items, total) = await directory.SearchAsync(user.Id, intent, keyword, page, pageSize, cancellationToken);
        return new(items.Select(x => new RoommateDirectoryCandidateDto(x.User.Id, x.User.DisplayName, x.User.AvatarPath,
            x.User.School, x.User.PreferredArea, x.User.MaxBudget, x.User.SleepHabit, x.User.PetPreference,
            x.User.SmokingPreference, x.Profile.Intent, x.Profile.Introduction, Score(user, x.User))).ToArray(), total, page, pageSize);
    }
    private static int? Score(UserProfile a, UserProfile b)
    {
        var known = 0; var matched = 0;
        void Compare(int left, int right) { if (left == 0 || right == 0) return; known++; if (left == right) matched++; }
        Compare((int)a.SleepHabit, (int)b.SleepHabit); Compare((int)a.PetPreference, (int)b.PetPreference);
        Compare((int)a.SmokingPreference, (int)b.SmokingPreference);
        return known == 0 ? null : (int)Math.Round(100m * matched / known);
    }
}
