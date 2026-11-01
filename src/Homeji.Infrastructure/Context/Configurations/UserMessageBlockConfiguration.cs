using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class UserMessageBlockConfiguration : IEntityTypeConfiguration<UserMessageBlock>
{
    public void Configure(EntityTypeBuilder<UserMessageBlock> builder)
    {
        builder.ToTable("user_message_blocks", "homeji", table => table.HasCheckConstraint("CK_UserMessageBlock_DifferentUsers", "\"BlockerId\" <> \"BlockedId\""));
        builder.HasKey(block => new { block.BlockerId, block.BlockedId });
        builder.HasIndex(block => new { block.BlockedId, block.BlockerId });
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(block => block.BlockerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(block => block.BlockedId).OnDelete(DeleteBehavior.Cascade);
    }
}
