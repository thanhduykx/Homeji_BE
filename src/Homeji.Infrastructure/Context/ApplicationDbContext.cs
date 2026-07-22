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
    public DbSet<RentalReview> RentalReviews => Set<RentalReview>();
    public DbSet<MarketplacePost> MarketplacePosts => Set<MarketplacePost>();
    public DbSet<MarketplacePostMedia> MarketplacePostMedia => Set<MarketplacePostMedia>();
    public DbSet<SavedPost> SavedPosts => Set<SavedPost>();
    public DbSet<RoommateInvitation> RoommateInvitations => Set<RoommateInvitation>();
    public DbSet<RoommateConversation> RoommateConversations => Set<RoommateConversation>();
    public DbSet<RoommateMessage> RoommateMessages => Set<RoommateMessage>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<BadWord> BadWords => Set<BadWord>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ViewingAppointment> ViewingAppointments => Set<ViewingAppointment>();
    public DbSet<LandlordVerificationRequest> LandlordVerificationRequests => Set<LandlordVerificationRequest>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<PostConversation> PostConversations => Set<PostConversation>();
    public DbSet<PostMessage> PostMessages => Set<PostMessage>();
    public DbSet<PostMessageAttachment> PostMessageAttachments => Set<PostMessageAttachment>();
    public DbSet<RentalWantedPost> RentalWantedPosts => Set<RentalWantedPost>();
    public DbSet<MarketplaceOrder> MarketplaceOrders => Set<MarketplaceOrder>();
    public DbSet<WalletAccount> WalletAccounts => Set<WalletAccount>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<WalletWithdrawalRequest> WalletWithdrawalRequests => Set<WalletWithdrawalRequest>();
    public DbSet<MarketplaceSellerSubscription> MarketplaceSellerSubscriptions => Set<MarketplaceSellerSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
