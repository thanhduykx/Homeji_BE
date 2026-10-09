using System.Diagnostics.Metrics;

namespace Homeji.Application.Services.AI;

public static class AiSearchTelemetry
{
    private static readonly Meter SearchMeter = new("Homeji.AiSearch", "1.0.0");
    public static readonly Counter<long> Requests = SearchMeter.CreateCounter<long>("homeji.ai_search.requests");
    public static readonly Counter<long> ParserFallbacks = SearchMeter.CreateCounter<long>("homeji.ai_search.parser_fallbacks");
    public static readonly Histogram<double> RetrievalDuration = SearchMeter.CreateHistogram<double>("homeji.ai_search.retrieval_duration", "ms");
    public static readonly Histogram<int> ResultCounts = SearchMeter.CreateHistogram<int>("homeji.ai_search.result_count", "{listing}");
}
