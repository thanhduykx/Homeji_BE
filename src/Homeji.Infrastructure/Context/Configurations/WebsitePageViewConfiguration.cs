using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class WebsitePageViewConfiguration : IEntityTypeConfiguration<WebsitePageView>
{
    public void Configure(EntityTypeBuilder<WebsitePageView> builder)
    {
        builder.ToTable("website_page_views", "homeji");
        builder.HasKey(view => view.Id);
        builder.Property(view => view.Page).HasMaxLength(32).IsRequired();
        builder.HasIndex(view => view.OccurredAt);
        builder.HasIndex(view => new { view.SessionId, view.OccurredAt });
    }
}
