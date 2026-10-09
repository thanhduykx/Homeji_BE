using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Homeji.Infrastructure.Context.Configurations;
public sealed class RoommateProfileConfiguration : IEntityTypeConfiguration<RoommateProfile>
{
    public void Configure(EntityTypeBuilder<RoommateProfile> builder)
    {
        builder.ToTable("roommate_profiles", "homeji");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.Intent).HasConversion<int>();
        builder.Property(x => x.Introduction).HasMaxLength(600);
        builder.HasOne<UserProfile>().WithOne().HasForeignKey<RoommateProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.IsDiscoverable, x.Intent, x.UpdatedAt });
    }
}
