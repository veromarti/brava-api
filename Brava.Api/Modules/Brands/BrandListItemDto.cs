namespace Brava.Api.Modules.Brands;

// Id is included even though this is the public listing endpoint's shape —
// it's not sensitive, and the admin UI's brand picker reuses this same
// endpoint rather than duplicating it behind auth just to expose an id.
public record BrandListItemDto(
    Guid Id,
    string Slug,
    string Name
);