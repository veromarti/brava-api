namespace Brava.Api.Modules.Metrics;

/// <summary>
/// The actual rows behind the health counts in <see cref="CatalogueMetricsDto"/>
/// — backs the click-through on each health card in /admin/metrics so an admin
/// can jump straight to the product/variant that needs fixing. Same
/// always-current snapshot as the summary, no period filter. Each list uses the
/// same predicate as its matching count in <c>GetCatalogueMetrics</c>; the two
/// are kept in sync by hand (four small lists, a boutique catalogue).
/// </summary>
public record CatalogueHealthDetailsDto(
    IReadOnlyList<ProductHealthItemDto> ProductsWithoutImages,
    IReadOnlyList<ProductHealthItemDto> ProductsWithoutSellableVariant,
    IReadOnlyList<VariantHealthItemDto> OutOfStockActiveVariants,
    IReadOnlyList<VariantHealthItemDto> VariantsMissingCost);

public record ProductHealthItemDto(Guid ProductId, string Slug, string Name);

/// <summary>Tone/size fields are raw (not a pre-built label) so the frontend's
/// <c>variantLabel()</c> formats them the same way it does everywhere else.</summary>
public record VariantHealthItemDto(
    Guid VariantId,
    Guid ProductId,
    string ProductSlug,
    string ProductName,
    string? ToneCode,
    string? ToneName,
    int? Units,
    decimal? VolumeMl,
    decimal? MassG,
    int PhysicalStock);
