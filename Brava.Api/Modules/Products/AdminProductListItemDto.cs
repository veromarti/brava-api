namespace Brava.Api.Modules.Products;

/// <summary>
/// Admin listing shape — unlike the public GET /api/products, this includes
/// inactive products (an admin needs to find and reactivate/edit them) and
/// the raw Ids needed to route into edit forms.
/// </summary>
public record AdminProductListItemDto(
    Guid Id,
    string Slug,
    string Name,
    string BrandName,
    string CategoryName,
    bool IsActive);
