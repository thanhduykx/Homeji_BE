using FluentValidation;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.RentalPosts.Validation;

public sealed class AddRentalPostMediaDtoValidator : AbstractValidator<AddRentalPostMediaDto>
{
    public AddRentalPostMediaDtoValidator()
    {
        RuleFor(request => request.Bucket)
            .Must(bucket => bucket is "homeji-media" or "cloudinary")
            .WithMessage("Ảnh phải thuộc kho Homeji hoặc Cloudinary.");

        RuleFor(request => request.Path)
            .NotEmpty()
            .MaximumLength(RentalPostMedia.MaxPathLength);

        When(request => request.Bucket == "cloudinary", () =>
        {
            RuleFor(request => request.Path)
                .Must(path => Uri.TryCreate(path, UriKind.Absolute, out var uri)
                    && uri.Scheme == Uri.UriSchemeHttps
                    && uri.Host == "res.cloudinary.com"
                    && string.IsNullOrEmpty(uri.UserInfo))
                .WithMessage("Ảnh Cloudinary phải dùng đường dẫn HTTPS của res.cloudinary.com.");
        });

        RuleFor(request => request.SortOrder)
            .GreaterThanOrEqualTo(0);
    }
}
