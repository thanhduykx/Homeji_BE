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

        builder.Property(profile => profile.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(profile => profile.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
