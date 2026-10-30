using System.Text.Json;
using Homeji.Application.DTOs.Moderation;
using Homeji.Application.Services.Moderation;
using Homeji.Domain.Entities;

namespace Homeji.Application.UnitTests.AI;

public sealed class AiListingReviewPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-30T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    [Fact] public void Batch_reserves_before_network_and_enforces_spacing_pause_and_daily_budget()
    {
        var schedule = new AiModerationSchedule();
        Assert.True(schedule.Reserve(2000, Now));
        Assert.False(schedule.Reserve(2000, Now.AddSeconds(59)));
        Assert.True(schedule.Reserve(2000, Now.AddSeconds(60)));
        Assert.True(schedule.Reserve(2000, Now.AddSeconds(120)));
        Assert.Equal(Now.AddSeconds(120).AddMinutes(10), schedule.NextAllowedAt);
        Assert.Equal(3, schedule.RequestsToday); Assert.Equal(6000, schedule.TokensReservedToday);
        schedule.DailyRequestLimit = 3;
        Assert.False(schedule.Reserve(2000, Now.AddHours(1)));
        Assert.Equal(Now.AddDays(1), schedule.NextAllowedAt);
        Assert.True(schedule.Reserve(2000, Now.AddDays(1)));
        Assert.Equal(1, schedule.RequestsToday);
    }
    [Fact] public void Token_budget_and_cooldown_survive_retries_without_refunding_uncertain_usage()
    {
        var schedule = new AiModerationSchedule { DailyTokenBudget = 10000 };
        Assert.True(schedule.Reserve(9000, Now));
        schedule.Backoff(Now.AddHours(1), "429");
        Assert.False(schedule.Reserve(100, Now.AddMinutes(30)));
        Assert.False(schedule.Reserve(2000, Now.AddHours(1)));
        Assert.Equal(9000, schedule.TokensReservedToday);
        schedule.Enabled = false; Assert.False(schedule.Reserve(100, Now.AddDays(1)));
    }
    [Fact] public void Expired_leases_are_recoverable_but_three_failures_go_to_a_human()
    {
        var job = new AiListingReview(Guid.NewGuid(), "food", "Tin", "hash", "{}", Now, Now);
        job.Claim(Now); Assert.Throws<Homeji.Domain.Exceptions.DomainException>(() => job.Claim(Now.AddMinutes(1)));
        job.Claim(Now.AddMinutes(4)); job.Retry(Now.AddHours(1), "429");
        Assert.Equal("pending", job.State); job.Claim(Now.AddHours(1)); job.Retry(Now.AddHours(2), "Unavailable");
        Assert.Equal("needs_review", job.State); Assert.Equal(3, job.Attempts);
    }
    [Theory]
    [InlineData("block", "illegal_drugs", "bán ma túy", .99, true)]
    [InlineData("review", "illegal_drugs", "bán ma túy", .99, false)]
    [InlineData("block", "illegal_drugs", "bán ma túy", .9, false)]
    [InlineData("block", "fraud", "bán ma túy", .99, false)]
    [InlineData("block", "illegal_drugs", "đoạn trích bịa", .99, false)]
    public void Auto_hide_requires_high_confidence_allowed_category_and_literal_evidence(string decision, string category, string evidence, double confidence, bool expected)
    {
        var source = JsonSerializer.Serialize(new { title = "bán ma túy", description = "tin kiểm thử" });
        Assert.Equal(expected, AiListingReviewPolicy.CanAutoBlock(new(decision, category, "Kiểm thử", evidence, confidence), source));
    }
    [Fact] public void Warnings_against_prohibited_goods_do_not_get_automatically_hidden()
    {
        var source = JsonSerializer.Serialize(new { title = "Cảnh báo: không bán ma túy", description = "Không đăng hàng cấm." });
        Assert.False(AiListingReviewPolicy.CanAutoBlock(new("block", "illegal_drugs", "Kiểm thử", "bán ma túy", 1), source));
    }
}
