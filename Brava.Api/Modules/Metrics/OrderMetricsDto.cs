namespace Brava.Api.Modules.Metrics;

public record OrderMetricsDto(
    DateTime? From,
    DateTime? To,
    /// <summary>Orders counted — Status == Entregado only. Pendiente/Confirmado/
    /// EnPreparacion/EnCamino haven't happened yet and could still change or be
    /// cancelled; Cancelado never counts. This is the "completed orders" basis
    /// for every money figure below.</summary>
    int CompletedOrdersCount,
    /// <summary>Sum of Subtotal (product sales only, no delivery fee).</summary>
    decimal Revenue,
    /// <summary>Sum of DeliveryFee.</summary>
    decimal DeliveryIncome,
    /// <summary>Revenue + DeliveryIncome — sum of Total.</summary>
    decimal TotalIncome,
    /// <summary>Sum of UnitCost * Quantity across order items. A null UnitCost
    /// counts as 0 here — see HasIncompleteCost.</summary>
    decimal Cogs,
    /// <summary>Revenue - Cogs. Only trustworthy when HasIncompleteCost is false.</summary>
    decimal GrossProfit,
    /// <summary>True if any counted order has a line item with no UnitCost —
    /// Cogs/GrossProfit are then a lower/upper bound, not exact.</summary>
    bool HasIncompleteCost);
