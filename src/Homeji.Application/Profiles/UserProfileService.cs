using FluentValidation;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Abstractions.Persistence;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.Profiles.Models;
using Homeji.Domain.Profiles;

namespace Homeji.Application.Profiles;

public sealed class UserProfileService : IUserProfileService
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserProfileRepository _repository;
    private readonly IValidator<UpdateMyProfileRequest> _validator;
    private readonly TimeProvider _timeProvider;

    public UserProfileService(
        ICurrentUser currentUser,
        IUserProfileRepository repository,
        IValidator<UpdateMyProfileRequest> validator,
        TimeProvider timeProvider)
    {
        _currentUser = currentUser;
        _repository = repository;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<UserProfileResponse> GetMyProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var profile = await _repository.GetByIdAsync(userId, cancellationToken);

        return profile is null
            ? throw new NotFoundException(nameof(UserProfile), userId)
            : Map(profile);
    }

    public async Task<UserProfileResponse> UpsertMyProfileAsync(
        UpdateMyProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray(),
                    StringComparer.Ordinal);

            throw new RequestValidationException(errors);
        }

        var userId = GetRequiredUserId();
        var now = _timeProvider.GetUtcNow();
        var profile = UserProfile.Create(userId, request.DisplayName!, now);
        var persistedProfile = await _repository.UpsertAsync(profile, cancellationToken);

        return Map(persistedProfile);
    }

    private Guid GetRequiredUserId()
    {
        return _currentUser.UserId is { } userId && userId != Guid.Empty
            ? userId
            : throw new UnauthorizedAccessException("The authenticated token does not contain a valid subject.");
    }

    private static UserProfileResponse Map(UserProfile profile)
    {
        return new UserProfileResponse(
            profile.Id,
            profile.DisplayName,
            profile.CreatedAt,
            profile.UpdatedAt);
    }
}
