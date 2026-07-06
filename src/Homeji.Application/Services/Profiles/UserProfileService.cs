using FluentValidation;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Profiles;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IServices.Profiles;
using Homeji.Application.Mappers.Profiles;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.Profiles;

public sealed class UserProfileService : IUserProfileService
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserProfileRepository _repository;
    private readonly IValidator<UpdateMyProfileDto> _validator;
    private readonly TimeProvider _timeProvider;

    public UserProfileService(
        ICurrentUser currentUser,
        IUserProfileRepository repository,
        IValidator<UpdateMyProfileDto> validator,
        TimeProvider timeProvider)
    {
        _currentUser = currentUser;
        _repository = repository;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<UserProfileDto> GetMyProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var profile = await _repository.GetByIdAsync(userId, cancellationToken);

        return profile is null
            ? throw new NotFoundException(nameof(UserProfile), userId)
            : UserProfileMapper.ToDto(profile);
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
        var profile = UserProfile.Create(userId, request.DisplayName!, now);
        var persistedProfile = await _repository.UpsertAsync(profile, cancellationToken);

        return UserProfileMapper.ToDto(persistedProfile);
    }

    private Guid GetRequiredUserId()
    {
        return _currentUser.UserId is { } userId && userId != Guid.Empty
            ? userId
            : throw new UnauthorizedAccessException("The authenticated token does not contain a valid subject.");
    }
}
