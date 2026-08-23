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
    decimal PriceTo,
    // True when any active variant has physical stock. A multi-variant product
    // can be true here but still show "Agotado" on one specific tone/size —
    // this is a listing-card signal, not per-variant detail (that's on
    // GET /api/products/{slug}).
    bool InStock,
    // The product's first image by DisplayOrder, or null if it has none yet.
    // Just a thumbnail for the card — the full gallery is on the detail page.
    string? ImageUrl);
