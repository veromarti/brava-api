namespace Brava.Api.Modules.Combos;

public record ComboDetailDto(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    bool IsActive,
    decimal OriginalPrice,
    decimal FinalPrice,
    string? ImageUrl,
    List<ComboItemDetailDto> Items);
