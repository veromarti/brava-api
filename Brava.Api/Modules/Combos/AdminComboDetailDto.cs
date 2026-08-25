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
    List<ComboItemDetailDto> Items);
