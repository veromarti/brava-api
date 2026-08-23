namespace Brava.Api.Modules.Categories;

/// <summary>Admin-facing shape — includes Id, unlike the public CategoryListItemDto.</summary>
public record CategoryDto(Guid Id, string Slug, string Name, int DisplayOrder, bool IsActive);
