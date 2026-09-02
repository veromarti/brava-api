namespace Brava.Api.Modules.Combos;

// ManualPrice is exposed here (unlike the public ComboDetailDto) so the
// admin edit form can tell "using the computed sum" apart from "someone
// typed an override" — the public shape only ever needs the two prices to
// render, not which one is authoritative.
public record AdminComboDetailDto(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    bool IsActive,
    decimal OriginalPrice,
    decimal? ManualPrice,
    decimal FinalPrice,
    // ImageUrl is the resolved image (kit's own, or the first member
    // product's as a fallback); HasOwnImage says whether a kit-specific one
    // is actually set — the edit form needs to tell those apart.
    string? ImageUrl,
    bool HasOwnImage,
    List<ComboItemDetailDto> Items);
