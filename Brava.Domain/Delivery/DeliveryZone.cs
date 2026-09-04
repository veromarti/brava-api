namespace Brava.Domain.Delivery;

/// <summary>
/// A delivery area with a flat fee. v1 zones are Medellín's comunas (seeded);
/// admins set the price per zone and can add more zones (nearby municipalities,
/// corregimientos) through the panel. Phase 3 will auto-detect the zone from a
/// typed address; for now the admin picks it when creating an order.
/// </summary>
public class DeliveryZone
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Flat delivery fee in whole COP. Seeded at 0 until an admin sets it.</summary>
    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
