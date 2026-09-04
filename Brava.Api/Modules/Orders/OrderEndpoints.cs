using System.Security.Claims;
using Brava.Application;
using Brava.Domain.Combos;
using Brava.Domain.Customers;
using Brava.Domain.Orders;
using Brava.Domain.Products;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Orders;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        // Admin-only — there's no customer-facing order flow yet (that's Phase 4).
        app.MapGet("/api/orders", GetOrders).RequireAuthorization();
        app.MapGet("/api/orders/{number}", GetOrderByNumber).RequireAuthorization();
        app.MapPost("/api/orders", CreateOrder).RequireAuthorization();
        app.MapPut("/api/orders/{number}/status", UpdateOrderStatus).RequireAuthorization();
        app.MapPut("/api/orders/{number}/payment", MarkOrderPaid).RequireAuthorization();
        return app;
    }

    private static async Task<Ok<List<OrderListItemDto>>> GetOrders(
        IBravaDbContext db, OrderStatus? status, PaymentStatus? paymentStatus)
    {
        var query = db.Orders.AsQueryable();
        if (status is not null)
        {
            query = query.Where(o => o.Status == status);
        }
        if (paymentStatus is not null)
        {
            query = query.Where(o => o.PaymentStatus == paymentStatus);
        }

        var orders = await query
            .OrderByDescending(o => o.Sequence)
            .Select(o => new OrderListItemDto(
                o.Id, o.Number, o.Status, o.PaymentStatus, o.ContactName, o.ContactPhone, o.Total, o.CreatedAt))
            .ToListAsync();

        return TypedResults.Ok(orders);
    }

    private static async Task<Results<Ok<OrderDetailDto>, NotFound<string>>> GetOrderByNumber(
        string number, IBravaDbContext db)
    {
        var order = await LoadFullOrderAsync(db, number);
        if (order is null)
        {
            return TypedResults.NotFound($"Order '{number}' not found.");
        }

        return TypedResults.Ok(ToDetailDto(order));
    }

    // Design calls made here, same time pressure as the rest of this admin
    // surface: a customer is found-or-created by phone (the natural key) so
    // every order builds history — there's no separate "guest order" path.
    // Order number is MAX(Sequence)+1, same accepted race as ComboEndpoints'
    // slug loop: this is a single-admin panel today, not a high-concurrency
    // checkout.
    private static async Task<Results<Created<OrderDetailDto>, NotFound<string>, BadRequest<string>>> CreateOrder(
        CreateOrderRequest request, IBravaDbContext db, ClaimsPrincipal user)
    {
        var contactName = request.ContactName.Trim();
        var contactPhone = request.ContactPhone.Trim();
        var deliveryAddress = request.DeliveryAddress.Trim();
        if (contactName.Length == 0 || contactPhone.Length == 0 || deliveryAddress.Length == 0)
        {
            return TypedResults.BadRequest("Nombre, teléfono y dirección son obligatorios.");
        }

        if (request.Items.Count == 0)
        {
            return TypedResults.BadRequest("El pedido necesita al menos un producto.");
        }

        foreach (var item in request.Items)
        {
            var hasVariant = item.ProductVariantId is not null;
            var hasCombo = item.ComboId is not null;
            if (hasVariant == hasCombo)
            {
                return TypedResults.BadRequest("Cada línea debe tener exactamente un producto o un kit.");
            }
            if (item.Quantity < 1)
            {
                return TypedResults.BadRequest("La cantidad debe ser al menos 1.");
            }
        }

        var deliveryFee = 0m;
        if (request.DeliveryZoneId is not null)
        {
            var zone = await db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == request.DeliveryZoneId);
            if (zone is null)
            {
                return TypedResults.NotFound($"Zona de envío '{request.DeliveryZoneId}' no encontrada.");
            }
            deliveryFee = zone.Price;
        }

        var variantIds = request.Items.Where(i => i.ProductVariantId is not null)
            .Select(i => i.ProductVariantId!.Value).Distinct().ToList();
        var comboIds = request.Items.Where(i => i.ComboId is not null)
            .Select(i => i.ComboId!.Value).Distinct().ToList();

        var variants = await db.ProductVariants
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id);
        var combos = await db.Combos
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant)
            .Where(c => comboIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        var missingVariant = variantIds.FirstOrDefault(id => !variants.ContainsKey(id));
        if (missingVariant != Guid.Empty)
        {
            return TypedResults.NotFound($"Producto '{missingVariant}' no encontrado.");
        }
        var missingCombo = comboIds.FirstOrDefault(id => !combos.ContainsKey(id));
        if (missingCombo != Guid.Empty)
        {
            return TypedResults.NotFound($"Kit '{missingCombo}' no encontrado.");
        }

        // Generated up front so OrderItem.OrderId (required) can be set in each
        // item's own initializer instead of patched in after construction.
        var orderId = Guid.NewGuid();
        var orderItems = new List<OrderItem>();
        foreach (var item in request.Items)
        {
            if (item.ProductVariantId is { } variantId)
            {
                var variant = variants[variantId];
                var error = VariantPricingError(variant);
                if (error is not null)
                {
                    return TypedResults.BadRequest(error);
                }

                orderItems.Add(new OrderItem
                {
                    OrderId = orderId,
                    ProductVariantId = variant.Id,
                    Description = VariantDescription(variant),
                    UnitPrice = variant.SellPrice!.Value,
                    UnitCost = variant.CostPrice,
                    Quantity = item.Quantity,
                    LineTotal = variant.SellPrice.Value * item.Quantity,
                });
            }
            else
            {
                var combo = combos[item.ComboId!.Value];
                var error = ComboPricingError(combo);
                if (error is not null)
                {
                    return TypedResults.BadRequest(error);
                }

                var (unitPrice, unitCost) = ComboPricing(combo);
                orderItems.Add(new OrderItem
                {
                    OrderId = orderId,
                    ComboId = combo.Id,
                    Description = combo.Name,
                    UnitPrice = unitPrice,
                    UnitCost = unitCost,
                    Quantity = item.Quantity,
                    LineTotal = unitPrice * item.Quantity,
                });
            }
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == contactPhone);
        var now = DateTime.UtcNow;
        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = contactName,
                Phone = contactPhone,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Customers.Add(customer);
        }

        var subtotal = orderItems.Sum(i => i.LineTotal);
        var maxSequence = await db.Orders.MaxAsync(o => (int?)o.Sequence) ?? 0;
        var sequence = maxSequence + 1;

        var order = new Order
        {
            Id = orderId,
            Number = $"BRA-{sequence:D4}",
            Sequence = sequence,
            Status = OrderStatus.Pendiente,
            PaymentStatus = PaymentStatus.Pendiente,
            CustomerId = customer.Id,
            ContactName = contactName,
            ContactPhone = contactPhone,
            DeliveryAddress = deliveryAddress,
            DeliveryZoneId = request.DeliveryZoneId,
            DeliveryFee = deliveryFee,
            Subtotal = subtotal,
            Total = subtotal + deliveryFee,
            Notes = request.Notes,
            CreatedByAdminId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
            CreatedAt = now,
            UpdatedAt = now,
        };
        order.Items = orderItems;

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var saved = await LoadFullOrderAsync(db, order.Number);
        return TypedResults.Created($"/api/orders/{order.Number}", ToDetailDto(saved!));
    }

    private static async Task<Results<Ok<OrderDetailDto>, NotFound<string>>> UpdateOrderStatus(
        string number, UpdateOrderStatusRequest request, IBravaDbContext db)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Number == number);
        if (order is null)
        {
            return TypedResults.NotFound($"Order '{number}' not found.");
        }

        order.Status = request.Status;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var saved = await LoadFullOrderAsync(db, order.Number);
        return TypedResults.Ok(ToDetailDto(saved!));
    }

    private static async Task<Results<Ok<OrderDetailDto>, NotFound<string>>> MarkOrderPaid(
        string number, MarkOrderPaidRequest request, IBravaDbContext db)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Number == number);
        if (order is null)
        {
            return TypedResults.NotFound($"Order '{number}' not found.");
        }

        order.PaymentStatus = PaymentStatus.Pagado;
        order.PaymentMethod = request.PaymentMethod;
        order.PaidAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var saved = await LoadFullOrderAsync(db, order.Number);
        return TypedResults.Ok(ToDetailDto(saved!));
    }

    private static Task<Order?> LoadFullOrderAsync(IBravaDbContext db, string number) =>
        db.Orders
            .Include(o => o.DeliveryZone)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Number == number);

    private static OrderDetailDto ToDetailDto(Order o) => new(
        o.Id, o.Number, o.Status, o.PaymentStatus, o.PaymentMethod, o.PaidAt, o.CustomerId,
        o.ContactName, o.ContactPhone, o.DeliveryAddress, o.DeliveryZoneId, o.DeliveryZone?.Name,
        o.DeliveryFee, o.Subtotal, o.Total, o.Notes, o.CreatedAt,
        o.Items.Select(i => new OrderItemDetailDto(
            i.Id, i.ProductVariantId, i.ComboId, i.Description, i.UnitPrice, i.UnitCost, i.Quantity, i.LineTotal))
            .ToList());

    // Active-only, mirroring VariantEndpoints' "can't activate without a sell
    // price" rule — an order line has to resolve to a real, sellable price.
    private static string? VariantPricingError(ProductVariant variant)
    {
        if (!variant.IsActive)
        {
            return $"El producto '{variant.Product.Name}' no está activo.";
        }
        if (variant.SellPrice is null)
        {
            return $"El producto '{variant.Product.Name}' no tiene precio de venta configurado.";
        }
        return null;
    }

    private static string? ComboPricingError(Combo combo)
    {
        if (!combo.IsActive)
        {
            return $"El kit '{combo.Name}' no está activo.";
        }
        if (combo.ManualPrice is null && combo.Items.Any(i => i.ProductVariant.SellPrice is null))
        {
            return $"El kit '{combo.Name}' no tiene precio configurado (falta precio en alguno de sus productos).";
        }
        return null;
    }

    private static (decimal UnitPrice, decimal? UnitCost) ComboPricing(Combo combo)
    {
        var unitPrice = combo.ManualPrice ?? combo.Items.Sum(i => i.ProductVariant.SellPrice!.Value);
        var unitCost = combo.Items.All(i => i.ProductVariant.CostPrice is not null)
            ? combo.Items.Sum(i => i.ProductVariant.CostPrice!.Value)
            : (decimal?)null;
        return (unitPrice, unitCost);
    }

    // "Labial Mate — Rojo (R01), 30 ml" — tone name+code, then size, only the parts that exist.
    private static string VariantDescription(ProductVariant variant)
    {
        var tone = variant.ToneName is not null && variant.ToneCode is not null
            ? $"{variant.ToneName} ({variant.ToneCode})"
            : variant.ToneName ?? variant.ToneCode;

        var size = variant.VolumeMl is not null ? $"{variant.VolumeMl} ml"
            : variant.MassG is not null ? $"{variant.MassG} g"
            : variant.Units is not null ? $"{variant.Units} u"
            : null;

        var parts = new List<string>();
        if (tone is not null) parts.Add(tone);
        if (size is not null) parts.Add(size);

        return parts.Count == 0 ? variant.Product.Name : $"{variant.Product.Name} — {string.Join(", ", parts)}";
    }
}
