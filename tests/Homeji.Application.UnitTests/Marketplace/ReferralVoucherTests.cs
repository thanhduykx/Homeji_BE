using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;
namespace Homeji.Application.UnitTests.Marketplace;
public sealed class ReferralVoucherTests
{
 [Theory]
 [InlineData("food",100000,5000)] [InlineData("food",500000,10000)] [InlineData("vip",99000,19800)] [InlineData("vip",500000,20000)]
 public void Discount_has_a_percentage_and_a_cap(string purpose, int amount, int expected)
 { var now = DateTimeOffset.UtcNow; var v = new ReferralVoucher(Guid.NewGuid(),Guid.NewGuid(),purpose,now); Assert.Equal(expected,v.Quote(amount,now)); }
 [Fact] public void Used_expired_or_below_minimum_vouchers_are_rejected_and_restore_keeps_expiry()
 {
  var now=DateTimeOffset.UtcNow; var v=new ReferralVoucher(Guid.NewGuid(),Guid.NewGuid(),"food",now); var expires=v.ExpiresAt;
  Assert.Throws<DomainException>(()=>v.Quote(99000,now)); Assert.Throws<DomainException>(()=>v.Quote(100000,expires));
  v.Use(Guid.NewGuid(),100000,now); Assert.Throws<DomainException>(()=>v.Quote(100000,now)); v.Restore(); Assert.Equal(expires,v.ExpiresAt); Assert.Equal(5000,v.Quote(100000,now));
 }
 [Fact] public void Food_discount_keeps_seller_net_and_refund_equal_to_actual_debit()
 {
  var now=DateTimeOffset.UtcNow; var o=new MarketplaceOrder(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),100000,now.AddHours(1),"Test address",null,now);
  o.ApplyVoucherDiscount(5000); Assert.Equal(95000,o.BuyerPaidAmount); Assert.Equal(90000,o.SellerNetAmount); Assert.Equal(5000,o.PlatformFeeAmount);
  Assert.Equal(o.BuyerPaidAmount,o.SellerNetAmount+o.PlatformFeeAmount); Assert.Throws<DomainException>(()=>o.ApplyVoucherDiscount(1));
 }
 [Fact] public void Budget_reserves_full_face_value_and_rejects_the_eleventh_pair()
 { var c=new ReferralCampaign(); for(var i=0;i<10;i++) c.ReservePair(); Assert.Equal(1000000,c.Reserved); Assert.Throws<DomainException>(c.ReservePair); }
}
