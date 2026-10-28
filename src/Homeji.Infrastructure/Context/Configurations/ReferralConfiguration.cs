using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Homeji.Infrastructure.Context.Configurations;
public sealed class ReferralCampaignConfiguration : IEntityTypeConfiguration<ReferralCampaign>
{
    public void Configure(EntityTypeBuilder<ReferralCampaign> builder) { builder.ToTable("referral_campaigns", "homeji"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasMaxLength(40); builder.Property(x => x.Budget).HasPrecision(18,0); builder.Property(x => x.Reserved).HasPrecision(18,0); builder.Property(x => x.Version).IsConcurrencyToken(); builder.HasData(new ReferralCampaign()); }
}
public sealed class ReferralAccountConfiguration : IEntityTypeConfiguration<ReferralAccount>
{
    public void Configure(EntityTypeBuilder<ReferralAccount> builder) { builder.ToTable("referral_accounts", "homeji"); builder.HasKey(x => x.UserId); builder.Property(x => x.Code).HasMaxLength(24); builder.HasIndex(x => x.Code).IsUnique(); builder.HasOne<UserProfile>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class ReferralClaimConfiguration : IEntityTypeConfiguration<ReferralClaim>
{
    public void Configure(EntityTypeBuilder<ReferralClaim> builder) { builder.ToTable("referral_claims", "homeji"); builder.HasKey(x => x.Id); builder.HasIndex(x => x.InviteeId).IsUnique(); builder.HasIndex(x => x.InviterId); builder.HasOne<UserProfile>().WithMany().HasForeignKey(x => x.InviterId).OnDelete(DeleteBehavior.Restrict); builder.HasOne<UserProfile>().WithMany().HasForeignKey(x => x.InviteeId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class ReferralVoucherConfiguration : IEntityTypeConfiguration<ReferralVoucher>
{
    public void Configure(EntityTypeBuilder<ReferralVoucher> builder) { builder.ToTable("referral_vouchers", "homeji"); builder.HasKey(x => x.Id); builder.HasIndex(x => x.OwnerId); builder.HasIndex(x => x.ReferenceId); builder.Property(x => x.Purpose).HasMaxLength(8); builder.Property(x => x.Discount).HasPrecision(18,0); builder.Property(x => x.Version).IsConcurrencyToken(); builder.Ignore(x => x.MaximumDiscount); builder.Ignore(x => x.MinimumSpend); builder.Ignore(x => x.Rate); builder.HasOne<UserProfile>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict); builder.HasOne<ReferralClaim>().WithMany().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Restrict); }
}
