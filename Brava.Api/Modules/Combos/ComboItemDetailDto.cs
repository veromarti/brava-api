namespace Brava.Api.Modules.Combos;

/// <summary>Enough to render "Product Name — Tone (30 ml)" and its own price for one combo item.</summary>
public record ComboItemDetailDto(
    Guid VariantId,
    string ProductSlug,
    string ProductName,
    string? ToneCode,
    string? ToneName,
    int? Units,
    decimal? VolumeMl,
    decimal? MassG,
    decimal SellPrice);
