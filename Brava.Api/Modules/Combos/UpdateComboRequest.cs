namespace Brava.Api.Modules.Combos;

// Slug immutable, same reasoning as UpdateProductRequest.
public record UpdateComboRequest(
    string Name,
    string Description,
    List<Guid> VariantIds,
    decimal? ManualPrice,
    bool IsActive);
