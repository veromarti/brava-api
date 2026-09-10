namespace Brava.Domain.Wishlists;

/// <summary>
/// A shareable gift wishlist. Created from the storefront's "Lista de deseos"
/// (previously browser-only, localStorage) so a shopper can share the link with
/// friends and family before a birthday/anniversary and they know exactly what
/// to buy. Reached by its short <see cref="Code"/> (e.g. /lista-de-deseos/A7K2QX):
/// no accounts (ADR-0005), anyone with the link can view, and the browser that
/// created it remembers the code to update it later.
///
/// Deliberately not FK-linked to the catalog — like the localStorage list it
/// replaces, an item is a slug + variant id + display snapshot, not an order
/// line. The gift page re-resolves the live price/availability by slug at view
/// time; the snapshots only cover a since-deleted product still rendering a name.
/// </summary>
public class Wishlist
{
    public Guid Id { get; set; }

    /// <summary>Short, URL-safe, case-sensitive lookup key. Unique.</summary>
    public required string Code { get; set; }

    public required string OwnerName { get; set; }

    /// <summary>Optional free-text note from the owner to whoever opens the list.</summary>
    public string? Note { get; set; }

    public List<WishlistItem> Items { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
