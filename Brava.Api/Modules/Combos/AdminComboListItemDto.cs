namespace Brava.Api.Modules.Combos;

public record AdminComboListItemDto(
    Guid Id,
    string Slug,
    string Name,
    bool IsActive,
    decimal OriginalPrice,
    decimal? ManualPrice,
    decimal FinalPrice,
    int ItemCount);
