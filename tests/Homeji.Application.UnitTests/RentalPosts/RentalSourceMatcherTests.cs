using Homeji.Application.DTOs.AI;
using Homeji.Application.Services.RentalPosts;
using Homeji.Domain.Entities;

namespace Homeji.Application.UnitTests.RentalPosts;

public sealed class RentalSourceMatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);
    private static int _sequence;

    private static RentalSourceListing Listing(string title, decimal price, string? description = null, string district = "thu-duc", decimal area = 20, string address = "Linh Trung, Thủ Đức")
    {
        var id = (++_sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var listing = RentalSourceListing.Create(id, $"https://phongtro123.com/{id}", title, address, district, price, area,
            ["https://pt123.cdn.static123.com/a.jpg"], Now, Now);
        if (description is not null) listing.RecordDetails(description, (string?)null, Now);
        return listing;
    }

    private static AiParsedSearchCriteriaDto Criteria(decimal? priceMax = null, string? location = null, string? keyword = null,
        IReadOnlyCollection<string>? required = null, IReadOnlyCollection<string>? excluded = null, IReadOnlyCollection<string>? unknown = null) =>
        new(location, keyword, null, priceMax, null, null, []) { RequiredAmenities = required ?? [], ExcludedAmenities = excluded ?? [], Unknown = unknown ?? [] };

    [Fact]
    public void Filters_by_budget_and_reports_the_full_total()
    {
        var rows = new[] { Listing("A", 1_500_000), Listing("B", 2_000_000), Listing("C", 3_500_000) };
        var result = RentalSourceMatcher.Match(rows, Criteria(priceMax: 2_000_000), 1);
        Assert.Equal(2, result.Total);
        Assert.Single(result.Rooms);
    }

    [Fact]
    public void Required_amenity_needs_evidence_in_the_description()
    {
        var withAc = Listing("Có mô tả", 2_000_000, "Phòng có máy lạnh, wifi");
        var silent = Listing("Không mô tả", 2_000_000);
        var result = RentalSourceMatcher.Match([withAc, silent], Criteria(required: ["AIR_CONDITIONER"]), 5);
        Assert.Equal(withAc.Id, Assert.Single(result.Rooms).Id);
    }

    [Theory]
    [InlineData("Không nuôi thú cưng")]
    [InlineData("Cấm nuôi mèo trong phòng")]
    public void Negated_amenity_does_not_count(string description)
    {
        var result = RentalSourceMatcher.Match([Listing("A", 2_000_000, description)], Criteria(required: ["PET_FRIENDLY"]), 5);
        Assert.Empty(result.Rooms);
    }

    [Fact]
    public void Excluded_amenity_removes_listings_that_state_it()
    {
        var result = RentalSourceMatcher.Match([Listing("A", 2_000_000, "Có bếp riêng")], Criteria(excluded: ["KITCHEN"]), 5);
        Assert.Empty(result.Rooms);
    }

    [Fact]
    public void Location_matches_without_accents_and_uses_the_district()
    {
        var quan9 = Listing("Phòng đẹp", 2_000_000, district: "quan-9", address: "Đường số 8");
        var thuDuc = Listing("Phòng khác", 2_000_000);
        var result = RentalSourceMatcher.Match([quan9, thuDuc], Criteria(location: "quan 9"), 5);
        Assert.Equal(quan9.Id, Assert.Single(result.Rooms).Id);
    }

    [Fact]
    public void Keyword_ignores_generic_words_and_reads_the_description()
    {
        var match = Listing("Phòng A", 2_000_000, "Gần chợ Thủ Đức, có ban công");
        var other = Listing("Phòng B", 2_000_000, "Yên tĩnh");
        var result = RentalSourceMatcher.Match([match, other], Criteria(keyword: "phòng trọ ban công"), 5);
        Assert.Equal(match.Id, Assert.Single(result.Rooms).Id);
    }

    [Fact]
    public void Unresolved_budget_returns_nothing_instead_of_guessing()
    {
        var result = RentalSourceMatcher.Match([Listing("A", 2_000_000)], Criteria(unknown: ["budget"]), 5);
        Assert.Empty(result.Rooms);
    }

    [Fact]
    public void Excerpt_never_contains_a_phone_number()
    {
        var room = RentalSourceMatcher.Match([Listing("A", 2_000_000, "Liên hệ 0901 234 567 để xem phòng")], Criteria(), 5).Rooms.Single();
        Assert.DoesNotContain("567", room.Excerpt, StringComparison.Ordinal);
    }
}
