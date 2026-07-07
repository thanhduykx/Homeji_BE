using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles", "homeji");

        builder.HasKey(profile => profile.Id)
            .HasName("pk_user_profiles");

        builder.Property(profile => profile.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(profile => profile.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(UserProfile.MaxDisplayNameLength)
            .IsRequired();

        builder.Property(profile => profile.Role)
            .HasColumnName("role")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(profile => profile.Phone)
            .HasColumnName("phone")
            .HasMaxLength(UserProfile.MaxPhoneLength);

        builder.Property(profile => profile.AvatarPath)
            .HasColumnName("avatar_path")
            .HasMaxLength(UserProfile.MaxAvatarPathLength);

        builder.Property(profile => profile.School)
            .HasColumnName("school")
            .HasMaxLength(UserProfile.MaxSchoolLength);

        builder.Property(profile => profile.PreferredArea)
            .HasColumnName("preferred_area")
            .HasMaxLength(UserProfile.MaxAreaLength);

        builder.Property(profile => profile.SleepHabit)
            .HasColumnName("sleep_habit")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(profile => profile.PetPreference)
            .HasColumnName("pet_preference")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(profile => profile.SmokingPreference)
            .HasColumnName("smoking_preference")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(profile => profile.MaxBudget)
            .HasColumnName("max_budget")
            .HasPrecision(18, 2);

        builder.Property(profile => profile.OnboardingCompleted)
            .HasColumnName("onboarding_completed")
            .IsRequired();

        builder.Property(profile => profile.LandlordVerificationStatus)
            .HasColumnName("landlord_verification_status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(profile => profile.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(profile => profile.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
