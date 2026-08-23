namespace Brava.Api.Modules.Categories;

// Id included for the same reason as BrandListItemDto — not sensitive, and
// avoids a duplicate gated endpoint just for the admin category picker.
public record CategoryListItemDto(Guid Id, string Slug, string Name, int DisplayOrder);
