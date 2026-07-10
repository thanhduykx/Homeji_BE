using FluentValidation;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Profiles;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IServices.Profiles;
using Homeji.Application.Mappers.Profiles;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.Profiles;

public sealed class UserProfileService : IUserProfileService
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserProfileRepository _repository;
    private readonly IUserSubscriptionRepository _subscriptions;
    private readonly IValidator<UpdateMyProfileDto> _validator;
    private readonly IValidator<UpdateLifestyleDto> _lifestyleValidator;
    private readonly TimeProvider _timeProvider;

    public UserProfileService(
        ICurrentUser currentUser,
        IUserProfileRepository repository,
        IUserSubscriptionRepository subscriptions,
        IValidator<UpdateMyProfileDto> validator,
        IValidator<UpdateLifestyleDto> lifestyleValidator,
        TimeProvider timeProvider)
    {
        _currentUser = currentUser;
        _repository = repository;
        _subscriptions = subscriptions;
        _validator = validator;
        _lifestyleValidator = lifestyleValidator;
        _timeProvider = timeProvider;
    }

    public async Task<UserProfileDto> GetMyProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var profile = await _repository.GetByIdAsync(userId, cancellationToken);
        var premium = await _subscriptions.GetActivePremiumAsync(userId, _timeProvider.GetUtcNow(), cancellationToken);

        return profile is null
            ? throw new NotFoundException(nameof(UserProfile), userId)
            : UserProfileMapper.ToDto(profile, premium is not null, premium?.ExpiresAt);
    }

    public async Task<UserProfileDto> UpsertMyProfileAsync(
        UpdateMyProfileDto request,
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
        var profile = await _repository.GetByIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            profile = Domain.Entities.UserProfile.Create(userId, request.DisplayName!, now);
        }

        profile.UpdateProfile(
            request.DisplayName!,
            request.Phone,
            request.AvatarPath,
            request.School,
            request.PreferredArea,
            now);

        var persistedProfile = await _repository.UpsertAsync(profile, cancellationToken);

        return UserProfileMapper.ToDto(persistedProfile);
    }

    public async Task<UserProfileDto> UpdateMyLifestyleAsync(
        UpdateLifestyleDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _lifestyleValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new RequestValidationException(validationResult.Errors
                .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray(),
                    StringComparer.Ordinal));
        }

        var userId = GetRequiredUserId();
        var profile = await _repository.GetByIdAsync(userId, cancellationToken)
            ?? throw new ForbiddenAccessException("Complete your profile before updating lifestyle.");

        profile.UpdateLifestyle(
            request.Role,
            request.SleepHabit,
            request.PetPreference,
            request.SmokingPreference,
            request.MaxBudget,
            request.PreferredArea,
            _timeProvider.GetUtcNow());

        await _repository.SaveAsync(profile, cancellationToken);
        return UserProfileMapper.ToDto(profile);
    }

    private Guid GetRequiredUserId()
    {
        return _currentUser.UserId is { } userId && userId != Guid.Empty
            ? userId
            : throw new UnauthorizedAccessException("The authenticated token does not contain a valid subject.");
    }
}
