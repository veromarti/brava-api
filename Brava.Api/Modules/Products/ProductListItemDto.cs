namespace Brava.Api.Modules.Products;

/// <summary>
/// ADR-0003: no PriceFrom/PriceTo on Product itself — price lives on the variant,
/// so this shape is the aggregate the listing page renders "Desde $X" or "$X" from
/// (PriceFrom == PriceTo means show a single price).
/// </summary>
public record ProductListItemDto(
    string Slug,
    string Name,
    string BrandName,
    string CategoryName,
    decimal PriceFrom,
    decimal PriceTo);
