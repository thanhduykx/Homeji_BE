using FluentValidation;
using Homeji.Application.DTOs.Marketplace;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Marketplace.Validation;

public sealed class UpsertMarketplacePostDtoValidator : AbstractValidator<UpsertMarketplacePostDto>
{
    public UpsertMarketplacePostDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề là bắt buộc.")
            .MaximumLength(MarketplacePost.MaxTitleLength).WithMessage($"Tiêu đề không được vượt quá {MarketplacePost.MaxTitleLength} ký tự.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Mô tả là bắt buộc.")
            .MaximumLength(MarketplacePost.MaxDescriptionLength).WithMessage($"Mô tả không được vượt quá {MarketplacePost.MaxDescriptionLength} ký tự.");

        When(x => x.ListingType == MarketplaceListingType.SecondHand, () =>
        {
            RuleFor(x => x.Condition)
                .NotEmpty().WithMessage("Tình trạng sản phẩm là bắt buộc.")
                .MaximumLength(MarketplacePost.MaxConditionLength).WithMessage($"Tình trạng không được vượt quá {MarketplacePost.MaxConditionLength} ký tự.");
        });

        When(x => x.ListingType == MarketplaceListingType.Food, () =>
        {
            RuleFor(x => x.Condition)
                .MaximumLength(MarketplacePost.MaxConditionLength)
                .When(x => !string.IsNullOrEmpty(x.Condition))
                .WithMessage($"Tình trạng không được vượt quá {MarketplacePost.MaxConditionLength} ký tự.");

            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(10_000m)
                .WithMessage("Giá món ăn tối thiểu là 10.000đ.");

            RuleFor(x => x.PreparationMinutes)
                .InclusiveBetween(MarketplacePost.MinFoodPreparationMinutes, MarketplacePost.MaxFoodPreparationMinutes)
                .When(x => x.PreparationMinutes.HasValue)
                .WithMessage($"Thời gian chuẩn bị món phải từ {MarketplacePost.MinFoodPreparationMinutes} đến {MarketplacePost.MaxFoodPreparationMinutes} phút.");

            RuleFor(x => x.EstimatedCost)
                .GreaterThanOrEqualTo(0)
                .When(x => x.EstimatedCost.HasValue)
                .WithMessage("Giá vốn ước tính không được âm.");

            RuleFor(x => x.DailyQuota)
                .InclusiveBetween(1, MarketplacePost.MaxFoodStock)
                .When(x => x.DailyQuota.HasValue)
                .WithMessage($"Định mức mỗi ngày phải từ 1 đến {MarketplacePost.MaxFoodStock}.");

            RuleFor(x => x.FreshnessBadge)
                .MaximumLength(MarketplacePost.MaxFreshnessBadgeLength)
                .When(x => !string.IsNullOrEmpty(x.FreshnessBadge))
                .WithMessage($"Nhãn độ tươi không được vượt quá {MarketplacePost.MaxFreshnessBadgeLength} ký tự.");

            RuleFor(x => x.AllergenAlerts)
                .MaximumLength(MarketplacePost.MaxAllergenAlertsLength)
                .When(x => !string.IsNullOrEmpty(x.AllergenAlerts))
                .WithMessage($"Cảnh báo dị ứng không được vượt quá {MarketplacePost.MaxAllergenAlertsLength} ký tự.");
        });

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Giá sản phẩm phải lớn hơn 0.")
            .Must(p => decimal.Truncate(p) == p).WithMessage("Giá phải là số nguyên.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Danh mục là bắt buộc.")
            .MaximumLength(MarketplacePost.MaxCategoryLength);

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Địa chỉ là bắt buộc.")
            .MaximumLength(MarketplacePost.MaxAddressLength);

        RuleFor(x => x.MediaUrls)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Ảnh là bắt buộc.")
            .Must(m => m != null && m.Count >= 1 && m.Count <= MarketplacePost.MaxMediaCount)
            .WithMessage($"Cần cung cấp từ 1 đến {MarketplacePost.MaxMediaCount} ảnh.");
    }
}
