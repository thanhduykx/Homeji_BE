using System.Text.Json;
using System.Text.Json.Nodes;
using Homeji.Application.DTOs.AI;
using Homeji.Application.Services.AI;

namespace Homeji.Application.UnitTests.AI;

/// <summary>Fifty labeled sentences: 25 independently specified intents, each with and without accents.</summary>
public sealed class VietnameseIntentBenchmarkTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEnumerable<object[]> LabeledSentences()
    {
        (string Text, string Expected)[] fixtures =
        [
            ("Phòng dưới 3 triệu", """{"priceMax":3000000}"""),
            ("Phòng từ 2 đến 4tr", """{"priceMin":2000000,"priceMax":4000000}"""),
            ("Phòng tối đa 2,5 triệu", """{"priceMax":2500000}"""),
            ("Phòng dưới 3.000.000đ", """{"priceMax":3000000}"""),
            ("Phòng dưới 3000k", """{"priceMax":3000000}"""),
            ("Phòng cả phí dưới 4tr", """{"priceMax":4000000,"budgetBasis":"total","unknown":["feeUnits"]}"""),
            ("Phòng rẻ hơn", """{"unknown":["budget"]}"""),
            ("Phòng từ 4 đến 2tr", """{"priceMin":4000000,"priceMax":2000000,"unknown":["priceRange"]}"""),
            ("Phòng có bếp và máy lạnh", """{"requiredAmenities":["AIR_CONDITIONER","KITCHEN"]}"""),
            ("Phòng không cần máy lạnh có bếp", """{"requiredAmenities":["KITCHEN"]}"""),
            ("Phòng ưu tiên máy lạnh cần wifi", """{"criteria":["AIR_CONDITIONER"],"requiredAmenities":["WIFI"]}"""),
            ("Phòng không có máy lạnh cần bếp", """{"excludedAmenities":["AIR_CONDITIONER"],"requiredAmenities":["KITCHEN"]}"""),
            ("Phòng không muốn ở ghép", """{"excludeRoommateShare":true}"""),
            ("Phòng cho 2 người có giữ xe", """{"occupants":2,"requiredAmenities":["PARKING"]}"""),
            ("Phòng cho 21 người", """{"unknown":["occupants"]}"""),
            ("Phòng từ 20 đến 30 m²", """{"areaMin":20,"areaMax":30}"""),
            ("Phòng diện tích ít nhất 25,5 mét vuông", """{"areaMin":25.5}"""),
            ("Phòng dưới 30m2", """{"areaMax":30}"""),
            ("Phòng Thủ Đức dưới 4tr không ở ghép", """{"location":"Thủ Đức","priceMax":4000000,"excludeRoommateShare":true}"""),
            ("Phòng Quận 9 có wc riêng", """{"location":"Quận 9","requiredAmenities":["PRIVATE_BATHROOM"]}"""),
            ("Phòng gần FPT", """{"destination":"FPT","unknown":["commute","destination"]}"""),
            ("Phòng gần FPT Khu Công nghệ cao đi bộ dưới 20 phút", """{"destination":"FPT Khu Công nghệ cao","travelMode":"WALKING","maxCommuteMinutes":20,"unknown":["commute"]}"""),
            ("Phòng gần SPKT xe buýt dưới 30 phút", """{"destination":"Sư phạm Kỹ thuật","travelMode":"TRANSIT","maxCommuteMinutes":30,"unknown":["commute","destination"]}"""),
            ("Phòng gần UIT đi xe máy", """{"destination":"Công nghệ Thông tin","unknown":["commute","destination","motorcycleCoverage"]}"""),
            ("Phòng gần HUTECH đi ô tô không quá 15 phút có bếp", """{"destination":"HUTECH","travelMode":"DRIVING","maxCommuteMinutes":15,"requiredAmenities":["KITCHEN"],"unknown":["commute","destination"]}"""),
        ];
        foreach (var (text, expected) in fixtures)
        {
            yield return [text, expected];
            yield return [RentalSearchIntent.Normalize(text), expected];
        }
    }

    [Theory]
    [MemberData(nameof(LabeledSentences))]
    public void EveryFieldMatchesTheIndependentLabel(string text, string expectedFields)
    {
        var expected = JsonSerializer.SerializeToNode(new AiParsedSearchCriteriaDto(null, null, null, null, null, null, []), JsonOptions)!.AsObject();
        foreach (var (key, value) in JsonNode.Parse(expectedFields)!.AsObject()) expected[key] = value?.DeepClone();
        var actual = JsonSerializer.SerializeToNode(RentalSearchIntent.Apply(text), JsonOptions);
        Assert.True(JsonNode.DeepEquals(expected, actual), $"Input: {text}\nExpected: {expected}\nActual: {actual}");
    }
}
