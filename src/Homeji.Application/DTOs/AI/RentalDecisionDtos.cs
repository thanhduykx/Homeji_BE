namespace Homeji.Application.DTOs.AI;

public sealed record RentalCostScenarioDto(int Occupants = 1, decimal ElectricityKwh = 0,
    decimal WaterM3 = 0, string ElectricityUnit = "unknown", string WaterUnit = "unknown",
    string InternetUnit = "unknown", decimal? OtherMonthlyFees = null, decimal? OtherInitialFees = null,
    bool ElectricityFreeConfirmed = false, bool WaterFreeConfirmed = false, bool InternetFreeConfirmed = false,
    bool DepositFreeConfirmed = false);
public sealed record RentalCostEstimateDto(Guid PostId, decimal KnownMonthlySubtotal,
    decimal? EstimatedMonthlyTotal, decimal? InitialPayment, IReadOnlyCollection<string> Unknown,
    IReadOnlyCollection<string> Questions, string SourceType, DateTimeOffset UpdatedAt);
public sealed record CommuteDestinationDto(string Label, decimal Latitude, decimal Longitude,
    string Mode = "WALK", DateTimeOffset? DepartureTime = null);
public sealed record CommuteOriginDto(Guid PostId, decimal Latitude, decimal Longitude);
public sealed record CommuteEstimateDto(Guid PostId, int? DistanceMeters, decimal? DurationMinutes,
    string Mode, DateTimeOffset CalculatedAt, DateTimeOffset? DepartureTime, string Status);
public sealed record RentalDecisionRequestDto(IReadOnlyCollection<Guid> PostIds,
    RentalCostScenarioDto? Scenario = null, CommuteDestinationDto? Destination = null);
public sealed record RentalDecisionItemDto(Homeji.Application.DTOs.RentalPosts.RentalPostDto Post,
    RentalCostEstimateDto Cost, CommuteEstimateDto? Commute);
public sealed record RentalDecisionResponseDto(IReadOnlyCollection<RentalDecisionItemDto> Posts,
    IReadOnlyCollection<Guid> UnavailablePostIds, string Summary);
public sealed record RentalDraftPreviewRequestDto(Guid PostId, string Notes);
public sealed record RentalDraftPreviewDto(string Title, string Description,
    IReadOnlyCollection<string> Missing, string SourceType);
public sealed record AdminAssistantTaskDto(string Kind, Guid Id, string Label);
public sealed record AdminAssistantSummaryDto(DateTimeOffset CalculatedAt, int PendingPosts,
    int PendingReports, IReadOnlyCollection<AdminAssistantTaskDto> Tasks, string Summary);
