using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class BadWordConfiguration : IEntityTypeConfiguration<BadWord>
{
    public void Configure(EntityTypeBuilder<BadWord> builder)
    {
        builder.ToTable("bad_words", "homeji");
        builder.HasKey(word => word.Id).HasName("pk_bad_words");
        builder.Property(word => word.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(word => word.Value).HasColumnName("value").HasMaxLength(120).IsRequired();
        builder.Property(word => word.IsActive).HasColumnName("is_active").IsRequired();
        builder.HasIndex(word => word.Value).IsUnique().HasDatabaseName("ux_bad_words_value");
    }
}
