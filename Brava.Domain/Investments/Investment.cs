namespace Brava.Domain.Investments;

/// <summary>
/// CompraInventario is excluded from GetOrderMetrics' NetBalance — that cost
/// is already recognized as COGS when the purchased stock sells (OrderItem.
/// UnitCost), so counting it again here would double-count the same peso.
/// It still gets its own row for cash-flow visibility (money spent on stock
/// before it sells), it's just not part of the profit calculation. Every
/// other category is a real cost with no other accounting entry, so it's
/// subtracted in full.
/// </summary>
public enum InvestmentCategory
{
    CompraInventario,
    Marketing,
    Equipo,
    Arriendo,
    Otro,
}

/// <summary>
/// A business cost not tied to any specific order — marketing, equipment,
/// supplies bought in bulk, subscriptions, rent, etc. Tracked so the
/// financial metrics can report a realistic net balance (gross profit minus
/// these), not just order-level numbers. Admin-entered, no soft-delete flag
/// like PackagingOption/DeliveryZone — nothing else references an
/// investment, so a mistaken entry is just deleted outright.
/// </summary>
public class Investment
{
    public Guid Id { get; set; }

    public required string Description { get; set; }

    public InvestmentCategory Category { get; set; }

    /// <summary>Cost in whole COP.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// When the cost was incurred — what Métricas' date-range filter checks
    /// against, independent of <see cref="CreatedAt"/> (an admin logging last
    /// month's rent today shouldn't have it land in this month's balance).
    /// </summary>
    public DateTime Date { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
