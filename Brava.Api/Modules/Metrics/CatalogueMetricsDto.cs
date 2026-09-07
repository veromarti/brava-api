namespace Brava.Api.Modules.Metrics;

public record CatalogueMetricsDto(
    int TotalProducts,
    int ActiveProducts,
    int InactiveProducts,
    int ProductsWithoutImages,
    /// <summary>ADR-0003: active + priced. A product can be "active" and still
    /// have nothing sellable if every variant is inactive or unpriced.</summary>
    int ProductsWithoutSellableVariant,
    int TotalActiveVariants,
    int OutOfStockActiveVariants,
    int VariantsMissingCost,
    /// <summary>Average of (SellPrice - CostPrice) / SellPrice across active
    /// variants that have both prices set, as a percentage. Null when no
    /// variant has both — "costo incompleto", not zero margin.</summary>
    decimal? AverageMarginPercent,
    int TotalCombos,
    int ActiveCombos,
    int CombosWithIncompleteCost);
