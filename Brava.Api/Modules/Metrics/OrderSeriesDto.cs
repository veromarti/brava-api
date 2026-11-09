namespace Brava.Api.Modules.Metrics;

public enum SeriesGranularity
{
    Day,
    Week,
    Month,
}

public record OrderSeriesPointDto(
    /// <summary>Start of this bucket — Monday for Week, the 1st for Month.</summary>
    DateTime PeriodStart,
    /// <summary>Sum of (Subtotal - DiscountAmount) for orders in this bucket.</summary>
    decimal Revenue,
    int OrdersCount);

public record OrderSeriesDto(DateTime From, DateTime To, SeriesGranularity Granularity, List<OrderSeriesPointDto> Points);
