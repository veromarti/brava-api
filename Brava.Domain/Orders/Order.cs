using Brava.Domain.Customers;
using Brava.Domain.Delivery;

namespace Brava.Domain.Orders;

public enum OrderStatus
{
    Pendiente,
    Confirmado,
    EnPreparacion,
    EnCamino,
    Entregado,
    Cancelado,
}

public enum PaymentStatus
{
    Pendiente,
    Pagado,
}

public enum PaymentMethod
{
    Efectivo,
    Transferencia,
}

/// <summary>
/// A sale. Created by an admin in the panel (v1 — the customer-facing flow is
/// still WhatsApp). Contact and delivery details are snapshotted so a guest
/// order stands alone and an account order isn't rewritten when the customer
/// later edits their profile. Money fields are whole COP stored as numeric(12,2)
/// for consistency with the rest of the schema.
/// </summary>
public class Order
{
    public Guid Id { get; set; }

    /// <summary>Human reference, "BRA-0001". Immutable; unique.</summary>
    public required string Number { get; set; }

    /// <summary>Integer behind <see cref="Number"/> — makes generation a MAX()+1 and sorting cheap.</summary>
    public int Sequence { get; set; }

    public OrderStatus Status { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    /// <summary>Null until payment is recorded, then Efectivo or Transferencia.</summary>
    public PaymentMethod? PaymentMethod { get; set; }

    public DateTime? PaidAt { get; set; }

    public Guid? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    // Snapshots — not derived from Customer, so they survive a guest order and
    // a later profile edit.
    public required string ContactName { get; set; }

    public required string ContactPhone { get; set; }

    public required string DeliveryAddress { get; set; }

    public Guid? DeliveryZoneId { get; set; }

    public DeliveryZone? DeliveryZone { get; set; }

    /// <summary>Snapshot of the zone's price at creation time.</summary>
    public decimal DeliveryFee { get; set; }

    /// <summary>Sum of the line totals.</summary>
    public decimal Subtotal { get; set; }

    /// <summary><see cref="Subtotal"/> + <see cref="DeliveryFee"/>.</summary>
    public decimal Total { get; set; }

    public string? Notes { get; set; }

    /// <summary>Which admin created the order (id only — no nav needed yet).</summary>
    public Guid CreatedByAdminId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
