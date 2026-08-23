using Brava.Api.Modules.Products.Images;

namespace Brava.Api.Modules.Products;

public record ProductDetailDto(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    string BrandName,
    string CategoryName,
    bool IsActive,
    List<ProductVariantDto> Variants,
    List<ImageDto> Images);
