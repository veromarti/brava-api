namespace Brava.Api.Modules.Products;

public record CreateProductRequest(
    string Name,
    string Description,
    Guid BrandId,
    Guid CategoryId);
