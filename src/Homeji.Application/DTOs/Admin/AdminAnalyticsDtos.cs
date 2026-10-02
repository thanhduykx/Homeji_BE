namespace Homeji.Application.DTOs.Admin;

public sealed record AdminProductAnalyticsDto(
    DateTimeOffset GeneratedAt,
    int PeriodDays,
    AdminProductKpisDto Kpis,
    IReadOnlyList<AdminAnalyticsTrendPointDto> Trend,
    IReadOnlyList<AdminAreaInsightDto> Areas);

public sealed record AdminProductKpisDto(
    int TotalUsers,
    int NewUsers,
    int ActiveListings,
    int NewListings,
    decimal MedianMonthlyPrice,
    decimal AveragePricePerSquareMeter,
    int Searches,
    int ListingViews,
    int Saves,
    int ViewingRequests,
    decimal SaveToViewRate,
    decimal ViewingRequestToSaveRate);

public sealed record AdminAnalyticsTrendPointDto(
    DateOnly Date,
    int NewUsers,
    int NewListings,
    int Searches,
    int ListingViews,
    int Saves,
    int ViewingRequests);

public sealed record AdminAreaInsightDto(
    string AreaName,
    double Latitude,
    double Longitude,
    int ActiveListings,
    decimal MedianMonthlyPrice,
    decimal AveragePricePerSquareMeter,
    int TotalViews,
    int TotalSaves,
    int ViewingRequests,
    decimal SaveRate,
    decimal DemandIndex,
    string PriceSignal,
    string Recommendation,
    string Confidence);
