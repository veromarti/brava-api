using Brava.Domain.Orders;

namespace Brava.Domain.Customers;

/// <summary>
/// A buyer. Orders don't require one (a guest order carries its own contact
/// snapshot), but when a customer exists their orders link here so they get a
/// history. Storefront login (PasswordHash + auth) is a later phase — for now
/// admins create customers while taking an order.
/// </summary>
public class Customer
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Unique — the natural key admins search by when taking an order.</summary>
    public required string Phone { get; set; }

    public string? Email { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
