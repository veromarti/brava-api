namespace Brava.Api.Modules.Products.Variants;

/// <summary>
/// Admin-facing shape — includes CostPrice, unlike the public ProductVariantDto.
/// </summary>
public record VariantDto(
    Guid Id,
    Guid ProductId,
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
