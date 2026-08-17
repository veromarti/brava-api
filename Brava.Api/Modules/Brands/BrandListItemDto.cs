namespace Brava.Api.Modules.Brands;

public record BrandListItemDto(
    string Slug,
    string Name
);
public record BrandListItemFilterDto(
    string Slug
);