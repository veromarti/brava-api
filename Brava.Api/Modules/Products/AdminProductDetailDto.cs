using Brava.Api.Modules.Products.Images;
using Brava.Api.Modules.Products.Variants;

namespace Brava.Api.Modules.Products;

/// <summary>
/// Admin-facing product detail. Like <see cref="ProductDetailDto"/> but
/// variants use the admin <see cref="VariantDto"/> (which carries CostPrice,
/// for margin/metrics views in the panel), and BrandId/CategoryId are
/// included so edit forms don't have to reverse-map them from names.
/// </summary>
public record AdminProductDetailDto(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    string BrandName,
    string CategoryName,
    Guid BrandId,
    Guid CategoryId,
    bool IsActive,
    List<VariantDto> Variants,
    List<ImageDto> Images);
