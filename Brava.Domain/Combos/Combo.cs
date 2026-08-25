namespace Brava.Domain.Combos;

/// <summary>
/// A kit of specific variants bundled at one price. Priced against real
/// variants (not products) for the same reason ADR-0003 puts price on
/// ProductVariant, not Product — a product with several tones/sizes has no
/// single price to sum, so a combo has to name exactly which variant of
/// each product it includes.
/// </summary>
public class Combo
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Slug { get; set; }

    /// <summary>Admin-authored — conditions, tone substitution notes, etc.</summary>
    public required string Description { get; set; }

    /// <summary>Null means "use the sum of item prices" — see ComboEndpoints for where that's computed.</summary>
    public decimal? ManualPrice { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<ComboItem> Items { get; set; } = new List<ComboItem>();
}
