namespace Brava.Domain.Wishlists;

/// <summary>
/// One line on a <see cref="Wishlist"/>. Mirrors the fields the browser
/// wishlist already carried: a product variant ("product") or a kit ("combo"),
/// identified by slug (+ variant id for products) so the gift page can look up
/// the current price, plus name/label/image/price snapshots for display.
/// </summary>
public class WishlistItem
{
    public Guid Id { get; set; }

    public required Guid WishlistId { get; set; }

    public Wishlist Wishlist { get; set; } = null!;

    /// <summary>"product" or "combo" — the two kinds the storefront wishlist uses.</summary>
    public required string ItemType { get; set; }

    /// <summary>Product slug or combo slug.</summary>
    public required string Slug { get; set; }

    /// <summary>The chosen product variant. Null for combo lines.</summary>
    public Guid? VariantId { get; set; }

    public int Quantity { get; set; }

    // --- display snapshots (taken when the line was added) -------------------

    public required string Name { get; set; }

    /// <summary>e.g. "Rojo (R01) · 30 ml". Null when the variant has no label / for combos.</summary>
    public string? VariantLabel { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Unit price as shown when the line was added, in whole COP.</summary>
    public decimal UnitPrice { get; set; }
}
