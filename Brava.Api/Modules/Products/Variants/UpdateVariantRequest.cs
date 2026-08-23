namespace Brava.Api.Modules.Products.Variants;

/// <summary>Full replace, not a patch — simplest shape for the MVP deadline.</summary>
public record UpdateVariantRequest(
    string? Sku,
    string? ToneCode,
    string? ToneName,
    int? Units,
    decimal? VolumeMl,
    decimal? MassG,
    decimal? CostPrice,
    decimal? SellPrice,
    int PhysicalStock,
    bool AvailableOnDemand,
    bool IsActive);
