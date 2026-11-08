namespace Brava.Api.Modules.Metrics;

public record OrderMetricsDto(
    DateTime? From,
    DateTime? To,
    /// <summary>Orders counted — Status == Entregado only. Pendiente/Confirmado/
    /// EnPreparacion/EnCamino haven't happened yet and could still change or be
    /// cancelled; Cancelado never counts. This is the "completed orders" basis
    /// for every money figure below.</summary>
    int CompletedOrdersCount,
    /// <summary>Sum of (Subtotal - DiscountAmount) — product sales actually
    /// charged, no delivery fee. Net of discounts so a discounted order
    /// doesn't overstate real income.</summary>
    decimal Revenue,
    /// <summary>Sum of DeliveryFee.</summary>
    decimal DeliveryIncome,
    /// <summary>Revenue + DeliveryIncome — sum of Total.</summary>
    decimal TotalIncome,
    /// <summary>Sum of DiscountAmount across counted orders — how much was
    /// given away, for visibility alongside Revenue.</summary>
    decimal TotalDiscounts,
    /// <summary>Sum of UnitCost * Quantity across order items. A null UnitCost
    /// counts as 0 here — see HasIncompleteCost.</summary>
    decimal Cogs,
    /// <summary>Sum of PackagingCost — the bag/box used, an internal cost like
    /// Cogs, never part of Revenue/TotalIncome.</summary>
    decimal PackagingCost,
    /// <summary>Revenue - Cogs - PackagingCost. Only trustworthy when
    /// HasIncompleteCost is false.</summary>
    decimal GrossProfit,
    /// <summary>Sum of Investment.Amount in this same From/To window (filtered
    /// on Investment.Date, not Order status — investments aren't orders) —
    /// marketing, equipment, rent, and other costs not tied to a single sale.
    /// Excludes the CompraInventario category: that cost is already in Cogs
    /// once the stock sells, so including it here too would double-count it.</summary>
    decimal TotalInvestments,
    /// <summary>GrossProfit - TotalInvestments — the realistic bottom line for
    /// the period, not just order-level profit. Only trustworthy when
    /// HasIncompleteCost is false.</summary>
    decimal NetBalance,
    /// <summary>True if any counted order has a line item with no UnitCost —
    /// Cogs/GrossProfit/NetBalance are then a lower/upper bound, not exact.</summary>
    bool HasIncompleteCost);
