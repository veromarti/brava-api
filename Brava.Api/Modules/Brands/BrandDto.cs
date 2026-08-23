namespace Brava.Api.Modules.Brands;

/// <summary>Admin-facing shape — includes Id, unlike the public BrandListItemDto.</summary>
public record BrandDto(Guid Id, string Slug, string Name, bool IsActive);
