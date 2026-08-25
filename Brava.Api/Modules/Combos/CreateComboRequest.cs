namespace Brava.Api.Modules.Combos;

public record CreateComboRequest(
    string Name,
    string Description,
    List<Guid> VariantIds,
    decimal? ManualPrice);
