using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class RoommateInvitationConfiguration : IEntityTypeConfiguration<RoommateInvitation>
{
    public void Configure(EntityTypeBuilder<RoommateInvitation> builder)
    {
        builder.ToTable("roommate_invitations", "homeji");
        builder.HasKey(invitation => invitation.Id).HasName("pk_roommate_invitations");
        builder.Property(invitation => invitation.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(invitation => invitation.RentalPostId).HasColumnName("rental_post_id").IsRequired();
        builder.Property(invitation => invitation.SenderId).HasColumnName("sender_id").IsRequired();
        builder.Property(invitation => invitation.ReceiverId).HasColumnName("receiver_id").IsRequired();
        builder.Property(invitation => invitation.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(invitation => invitation.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(invitation => invitation.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(invitation => new { invitation.RentalPostId, invitation.SenderId, invitation.ReceiverId, invitation.Status })
            .HasDatabaseName("ix_roommate_invitations_pending_lookup")
            .HasFilter($"status = {(int)RoommateInvitationStatus.Pending}");
        builder.HasIndex(invitation => invitation.ReceiverId).HasDatabaseName("ix_roommate_invitations_receiver_id");
        builder.HasIndex(invitation => invitation.SenderId).HasDatabaseName("ix_roommate_invitations_sender_id");
    }
}
