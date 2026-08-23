namespace Brava.Api.Modules.Products;

public record ProductDto(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    bool IsActive);
