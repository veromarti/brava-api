namespace Brava.Api.Modules.Wishlists;

// The wire contract mirrors the storefront's browser wishlist item shape, so
// the frontend can POST its existing localStorage lines almost as-is. Type is
// "product" | "combo" (plain string, not an enum) to match those literals.
public record WishlistItemPayload(
    string Type,
    string Slug,
    Guid? VariantId,
    string Name,
    string? VariantLabel,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity);

/// <summary>Body for both POST (create) and PUT (full replace).</summary>
public record SaveWishlistRequest(
    string OwnerName,
    string? Note,
    List<WishlistItemPayload> Items);

public record WishlistItemDto(
    Guid Id,
    string Type,
    string Slug,
    Guid? VariantId,
    string Name,
    string? VariantLabel,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    bool IsGifted);

public record WishlistDto(
    string Code,
    string OwnerName,
    string? Note,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<WishlistItemDto> Items);

public record CreateWishlistResponse(string Code);

/// <summary>
/// Marks one or more of this wishlist's lines as already gifted — sent by the
/// shared gift page right after a "Regalar esto"/"Regalar todo" order is
/// created, so a later visitor sees them as taken. Ids outside this wishlist
/// are silently ignored rather than erroring the whole batch.
/// </summary>
public record MarkItemsGiftedRequest(List<Guid> ItemIds);
