namespace Brava.Api.Modules.Products;

/// <summary>
/// Public-facing shape: no CostPrice. ADR-0007's derived-stock formula isn't
/// implemented (no reservations table yet), so PhysicalStock/AvailableOnDemand
/// are the raw fields — the frontend renders "Agotado" / "Disponible bajo
/// pedido" from these two, same as ADR-0007 describes for the eventual
/// available_stock case.
/// </summary>
public record ProductVariantDto(
    Guid Id,
    string? Sku,
    string? ToneCode,
    string? ToneName,
    int? Units,
    decimal? VolumeMl,
    decimal? MassG,
    decimal? SellPrice,
    int PhysicalStock,
    bool AvailableOnDemand,
    bool IsActive);
