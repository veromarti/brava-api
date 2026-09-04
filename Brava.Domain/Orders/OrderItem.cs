using Brava.Domain.Combos;
using Brava.Domain.Products;

namespace Brava.Domain.Orders;

/// <summary>
/// One line on an order. Exactly one of <see cref="ProductVariantId"/> /
/// <see cref="ComboId"/> is set — a kit is a single line, not its members
/// expanded. Every displayed/costed value is snapshotted at creation, so
/// later catalog edits (or a hard-deleted variant) never rewrite order
/// history; the FKs are SetNull for the same reason.
/// </summary>
public class OrderItem
{
    public Guid Id { get; set; }

    public required Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public Guid? ProductVariantId { get; set; }

    public ProductVariant? ProductVariant { get; set; }

    public Guid? ComboId { get; set; }

    public Combo? Combo { get; set; }

    /// <summary>e.g. "Labial Mate — Rojo (R01)" or "Kit Glow".</summary>
    public required string Description { get; set; }

    /// <summary>Variant sell price, or combo final price.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Variant cost price, or the sum of a kit's member-variant cost prices.
    /// Null when any component cost is missing — margin views flag those.
    /// </summary>
    public decimal? UnitCost { get; set; }

    public int Quantity { get; set; }

    /// <summary><see cref="UnitPrice"/> * <see cref="Quantity"/>.</summary>
    public decimal LineTotal { get; set; }
}
