namespace Brava.Api.Modules.Combos;

// OriginalPrice is the sum of item prices; FinalPrice is ManualPrice when
// set, otherwise the same as OriginalPrice. The frontend shows OriginalPrice
// crossed out only when it differs from FinalPrice.
public record ComboListItemDto(
    string Slug,
    string Name,
    decimal OriginalPrice,
    decimal FinalPrice,
    string? ImageUrl);
