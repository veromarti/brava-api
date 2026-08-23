namespace Brava.Api.Modules.Products.Variants;

public record CreateVariantRequest(
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
