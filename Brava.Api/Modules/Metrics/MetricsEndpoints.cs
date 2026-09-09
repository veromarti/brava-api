using Brava.Application;
using Brava.Domain.Orders;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Metrics;

public static class MetricsEndpoints
{
    public static IEndpointRouteBuilder MapMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/metrics/catalogue", GetCatalogueMetrics).RequireAuthorization();
        app.MapGet("/api/metrics/catalogue/details", GetCatalogueHealthDetails).RequireAuthorization();
        app.MapGet("/api/metrics/orders", GetOrderMetrics).RequireAuthorization();
        return app;
    }

    // Always the current snapshot — no period filter, unlike order metrics.
    // "Health" here means "would this hurt the storefront or the numbers",
    // not a general product report: missing images, nothing sellable, no
    // stock, or a cost gap that would silently break margin metrics.
    private static async Task<Ok<CatalogueMetricsDto>> GetCatalogueMetrics(IBravaDbContext db)
    {
        var totalProducts = await db.Products.CountAsync();
        var activeProducts = await db.Products.CountAsync(p => p.IsActive);
        var productsWithoutImages = await db.Products.CountAsync(p => !p.Images.Any());
        var productsWithoutSellableVariant = await db.Products
            .CountAsync(p => !p.Variants.Any(v => v.IsActive && v.SellPrice != null));

        var activeVariants = db.ProductVariants.Where(v => v.IsActive);
        var totalActiveVariants = await activeVariants.CountAsync();
        var outOfStockActiveVariants = await activeVariants
            .CountAsync(v => v.PhysicalStock <= 0 && !v.AvailableOnDemand);
        var variantsMissingCost = await activeVariants
            .CountAsync(v => v.SellPrice != null && v.CostPrice == null);

        // Materialized client-side — an average-of-ratios isn't SQL-translatable
        // as cleanly, and the active/priced/costed set is small (a boutique
        // catalogue, not a warehouse).
        var margins = await activeVariants
            .Where(v => v.SellPrice != null && v.SellPrice > 0 && v.CostPrice != null)
            .Select(v => (v.SellPrice!.Value - v.CostPrice!.Value) / v.SellPrice!.Value)
            .ToListAsync();
        decimal? averageMarginPercent = margins.Count > 0 ? margins.Average() * 100m : null;

        var totalCombos = await db.Combos.CountAsync();
        var activeCombos = await db.Combos.CountAsync(c => c.IsActive);
        var combosWithIncompleteCost = await db.Combos
            .CountAsync(c => c.Items.Any(i => i.ProductVariant.CostPrice == null));

        return TypedResults.Ok(new CatalogueMetricsDto(
            totalProducts,
            activeProducts,
            totalProducts - activeProducts,
            productsWithoutImages,
            productsWithoutSellableVariant,
            totalActiveVariants,
            outOfStockActiveVariants,
            variantsMissingCost,
            averageMarginPercent,
            totalCombos,
            activeCombos,
            combosWithIncompleteCost));
    }

    // The rows behind GetCatalogueMetrics's four health counts — each Where
    // clause mirrors the matching CountAsync above, so a card's list and its
    // number always agree. Ordered by product name so the list reads like the
    // products table. No paging: these are the *problem* rows, a short list by
    // definition, and the whole point is to clear them to zero.
    private static async Task<Ok<CatalogueHealthDetailsDto>> GetCatalogueHealthDetails(IBravaDbContext db)
    {
        var productsWithoutImages = await db.Products
            .Where(p => !p.Images.Any())
            .OrderBy(p => p.Name)
            .Select(p => new ProductHealthItemDto(p.Id, p.Slug, p.Name))
            .ToListAsync();

        var productsWithoutSellableVariant = await db.Products
            .Where(p => !p.Variants.Any(v => v.IsActive && v.SellPrice != null))
            .OrderBy(p => p.Name)
            .Select(p => new ProductHealthItemDto(p.Id, p.Slug, p.Name))
            .ToListAsync();

        var outOfStockActiveVariants = await db.ProductVariants
            .Where(v => v.IsActive && v.PhysicalStock <= 0 && !v.AvailableOnDemand)
            .OrderBy(v => v.Product.Name)
            .Select(v => new VariantHealthItemDto(
                v.Id, v.ProductId, v.Product.Slug, v.Product.Name,
                v.ToneCode, v.ToneName, v.Units, v.VolumeMl, v.MassG, v.PhysicalStock))
            .ToListAsync();

        var variantsMissingCost = await db.ProductVariants
            .Where(v => v.IsActive && v.SellPrice != null && v.CostPrice == null)
            .OrderBy(v => v.Product.Name)
            .Select(v => new VariantHealthItemDto(
                v.Id, v.ProductId, v.Product.Slug, v.Product.Name,
                v.ToneCode, v.ToneName, v.Units, v.VolumeMl, v.MassG, v.PhysicalStock))
            .ToListAsync();

        return TypedResults.Ok(new CatalogueHealthDetailsDto(
            productsWithoutImages,
            productsWithoutSellableVariant,
            outOfStockActiveVariants,
            variantsMissingCost));
    }

    // "Completed" = Entregado, per the business call this metric is built on:
    // Pendiente/Confirmado/EnPreparacion/EnCamino could still change or be
    // cancelled, so only a delivered order counts as a real sale. `from`/`to`
    // filter on CreatedAt; `to` is treated as inclusive of that whole day.
    private static async Task<Results<Ok<OrderMetricsDto>, BadRequest<string>>> GetOrderMetrics(
        IBravaDbContext db, DateTime? from, DateTime? to)
    {
        // Model binding parses the query string with Kind=Unspecified, but
        // CreatedAt is timestamptz (stored as UTC via DateTime.UtcNow) —
        // Npgsql refuses to compare against an Unspecified-kind value.
        // Treated as plain calendar-day boundaries in UTC, same simplicity
        // as the rest of this single-timezone (Colombia) admin panel.
        var fromUtc = from is null ? (DateTime?)null : DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc);
        var toUtc = to is null ? (DateTime?)null : DateTime.SpecifyKind(to.Value.Date, DateTimeKind.Utc);

        // Without this, a swapped from/to (a picker bug, or someone typing the
        // range backwards) silently returns an all-zero report that reads as
        // "no sales that period" instead of "invalid range".
        if (fromUtc is not null && toUtc is not null && fromUtc > toUtc)
        {
            return TypedResults.BadRequest("'from' no puede ser posterior a 'to'.");
        }

        var query = db.Orders.Where(o => o.Status == OrderStatus.Entregado);
        if (fromUtc is not null)
        {
            query = query.Where(o => o.CreatedAt >= fromUtc.Value);
        }
        if (toUtc is not null)
        {
            query = query.Where(o => o.CreatedAt < toUtc.Value.AddDays(1));
        }

        var orders = await query
            .Select(o => new
            {
                o.Subtotal,
                o.DeliveryFee,
                o.Total,
                o.PackagingCost,
                Items = o.Items.Select(i => new { i.UnitCost, i.Quantity }).ToList(),
            })
            .ToListAsync();

        var revenue = orders.Sum(o => o.Subtotal);
        var deliveryIncome = orders.Sum(o => o.DeliveryFee);
        var totalIncome = orders.Sum(o => o.Total);
        var packagingCost = orders.Sum(o => o.PackagingCost);
        var hasIncompleteCost = orders.Any(o => o.Items.Any(i => i.UnitCost is null));
        var cogs = orders.Sum(o => o.Items.Sum(i => (i.UnitCost ?? 0m) * i.Quantity));
        var grossProfit = revenue - cogs - packagingCost;

        return TypedResults.Ok(new OrderMetricsDto(
            from, to, orders.Count, revenue, deliveryIncome, totalIncome, cogs, packagingCost, grossProfit, hasIncompleteCost));
    }
}
