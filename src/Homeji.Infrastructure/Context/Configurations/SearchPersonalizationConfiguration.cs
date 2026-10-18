using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Homeji.Infrastructure.Context.Configurations;

public sealed class SearchPersonalizationConfiguration : IEntityTypeConfiguration<SearchPersonalization>
{
    public void Configure(EntityTypeBuilder<SearchPersonalization> builder)
    {
        builder.ToTable("search_personalization", "homeji");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.Data).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.Enabled, x.CheckedAt });
        builder.HasOne<UserProfile>().WithOne().HasForeignKey<SearchPersonalization>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
