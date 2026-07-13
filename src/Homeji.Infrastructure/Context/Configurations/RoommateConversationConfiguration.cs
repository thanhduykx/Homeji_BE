using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class RoommateConversationConfiguration : IEntityTypeConfiguration<RoommateConversation>
{
    public void Configure(EntityTypeBuilder<RoommateConversation> builder)
    {
        builder.ToTable("roommate_conversations", "homeji");
        builder.HasKey(conversation => conversation.Id).HasName("pk_roommate_conversations");
        builder.Property(conversation => conversation.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(conversation => conversation.InvitationId).HasColumnName("invitation_id").IsRequired();
        builder.Property(conversation => conversation.RentalPostId).HasColumnName("rental_post_id").IsRequired();
        builder.Property(conversation => conversation.FirstParticipantId).HasColumnName("first_participant_id").IsRequired();
        builder.Property(conversation => conversation.SecondParticipantId).HasColumnName("second_participant_id").IsRequired();
        builder.Property(conversation => conversation.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(conversation => conversation.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(conversation => conversation.InvitationId)
            .IsUnique()
            .HasDatabaseName("ux_roommate_conversations_invitation_id");
        builder.HasIndex(conversation => new { conversation.FirstParticipantId, conversation.UpdatedAt })
            .HasDatabaseName("ix_roommate_conversations_first_updated");
        builder.HasIndex(conversation => new { conversation.SecondParticipantId, conversation.UpdatedAt })
            .HasDatabaseName("ix_roommate_conversations_second_updated");
        builder.HasIndex(conversation => conversation.RentalPostId)
            .HasDatabaseName("ix_roommate_conversations_rental_post_id");
        builder.HasOne<RoommateInvitation>()
            .WithOne()
            .HasForeignKey<RoommateConversation>(conversation => conversation.InvitationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_roommate_conversations_invitations");
        builder.HasOne<RentalPost>()
            .WithMany()
            .HasForeignKey(conversation => conversation.RentalPostId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_roommate_conversations_rental_posts");
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(conversation => conversation.FirstParticipantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_roommate_conversations_first_participant");
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(conversation => conversation.SecondParticipantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_roommate_conversations_second_participant");
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_roommate_conversations_distinct_participants",
            "first_participant_id <> second_participant_id"));
    }
}
