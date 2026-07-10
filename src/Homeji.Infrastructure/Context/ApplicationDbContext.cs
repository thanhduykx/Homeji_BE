using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Context;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<RentalPost> RentalPosts => Set<RentalPost>();
    public DbSet<RentalPostMedia> RentalPostMedia => Set<RentalPostMedia>();
    public DbSet<RentalPostAmenity> RentalPostAmenities => Set<RentalPostAmenity>();
    public DbSet<SavedPost> SavedPosts => Set<SavedPost>();
    public DbSet<RoommateInvitation> RoommateInvitations => Set<RoommateInvitation>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<BadWord> BadWords => Set<BadWord>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
