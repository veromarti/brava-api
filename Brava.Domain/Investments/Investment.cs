namespace Brava.Domain.Investments;

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
