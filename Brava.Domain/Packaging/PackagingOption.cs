namespace Brava.Domain.Packaging;

/// <summary>
/// A bag/box option used to pack an order (e.g. "Bolsa", "Caja chica",
/// "Caja grande"). Its price is an internal cost — tracked for margin
/// metrics, never added to the customer-facing order total. Same
/// admin-managed shape as DeliveryZone.
/// </summary>
public class PackagingOption
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Internal cost in whole COP — not charged to the customer.</summary>
    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
