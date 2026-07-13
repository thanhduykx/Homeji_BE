using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class ViewingAppointmentConfiguration : IEntityTypeConfiguration<ViewingAppointment>
{
    public void Configure(EntityTypeBuilder<ViewingAppointment> builder)
    {
        builder.ToTable("viewing_appointments", "homeji");
        builder.HasKey(appointment => appointment.Id);
        builder.Property(appointment => appointment.Note).HasMaxLength(ViewingAppointment.MaxNoteLength);
        builder.Property(appointment => appointment.Status).HasConversion<int>();
        builder.HasIndex(appointment => new { appointment.RequesterId, appointment.CreatedAt });
        builder.HasIndex(appointment => new { appointment.OwnerId, appointment.CreatedAt });
        builder.HasIndex(appointment => new { appointment.RentalPostId, appointment.RequesterId, appointment.Status });
        builder.HasOne<RentalPost>()
            .WithMany()
            .HasForeignKey(appointment => appointment.RentalPostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
