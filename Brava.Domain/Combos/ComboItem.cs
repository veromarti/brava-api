using Brava.Domain.Products;

namespace Brava.Domain.Combos;

/// <summary>One variant included in a combo — no quantity; a variant listed twice is two rows.</summary>
public class ComboItem
{
    public Guid Id { get; set; }

    public required Guid ComboId { get; set; }

    public required Guid ProductVariantId { get; set; }

    public Combo Combo { get; set; } = null!;

    public ProductVariant ProductVariant { get; set; } = null!;
}
