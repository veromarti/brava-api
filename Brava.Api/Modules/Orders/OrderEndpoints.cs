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
    // CreateStorefrontOrder is anonymous, so these bound how much damage a
    // bad/abusive payload can do — same reasoning as WishlistEndpoints.
    private const int MaxStorefrontItems = 50;
    private const int MaxContactFieldLength = 200;
    private const int MaxNotesLength = 1000;

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        // Admin-only — the panel's own order entry.
        app.MapGet("/api/orders", GetOrders).RequireAuthorization();
        app.MapGet("/api/orders/{number}", GetOrderByNumber).RequireAuthorization();
        app.MapPost("/api/orders", CreateOrder).RequireAuthorization();
        app.MapPut("/api/orders/{number}", UpdateOrder).RequireAuthorization();
        app.MapPut("/api/orders/{number}/status", UpdateOrderStatus).RequireAuthorization();
        app.MapPut("/api/orders/{number}/payment", MarkOrderPaid).RequireAuthorization();
        app.MapPut("/api/orders/{number}/admin", AssignOrderAdmin).RequireAuthorization();
        // Anonymous — a customer hitting "Pedir por WhatsApp" on the storefront.
        app.MapPost("/api/orders/storefront", CreateStorefrontOrder);
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

        return TypedResults.Ok(await ToDetailDtoAsync(order, db));
    }

    // Design calls made here, same time pressure as the rest of this admin
    // surface: a customer is found-or-created by phone (the natural key) so
    // every order builds history — there's no separate "guest order" path.
    // Order number is MAX(Sequence)+1, same accepted race as ComboEndpoints'
    // slug loop: this is a single-admin panel today, not a high-concurrency
    // checkout.
    private static async Task<Results<Created<OrderDetailDto>, NotFound<string>, BadRequest<string>>> CreateOrder(
        CreateOrderRequest request, IBravaDbContext db)
    {
        var contactName = request.ContactName.Trim();
        var contactPhone = request.ContactPhone.Trim();
        var deliveryAddress = request.DeliveryAddress.Trim();
        if (contactName.Length == 0 || contactPhone.Length == 0 || deliveryAddress.Length == 0)
        {
            return TypedResults.BadRequest("Nombre, teléfono y dirección son obligatorios.");
        }

        var itemsShapeError = ValidateItemRequests(request.Items);
        if (itemsShapeError is not null)
        {
            return TypedResults.BadRequest(itemsShapeError);
        }

        var admin = await db.Admins.FirstOrDefaultAsync(a => a.Id == request.CreatedByAdminId && a.IsActive);
        if (admin is null)
        {
            return TypedResults.NotFound($"Admin '{request.CreatedByAdminId}' no encontrado o inactivo.");
        }

        var (deliveryFee, zoneError) = await ResolveDeliveryFeeAsync(request.DeliveryZoneId, db);
        if (zoneError is not null)
        {
            return TypedResults.NotFound(zoneError);
        }

        var (packagingCost, packagingError) = await ResolvePackagingCostAsync(request.PackagingOptionId, db);
        if (packagingError is not null)
        {
            return TypedResults.NotFound(packagingError);
        }

        var orderId = Guid.NewGuid();
        var (orderItems, itemsError) = await BuildOrderItemsAsync(request.Items, orderId, db);
        if (itemsError is not null)
        {
            return TypedResults.BadRequest(itemsError);
        }

        var customer = await FindOrCreateCustomerAsync(db, contactName, contactPhone);
        var subtotal = orderItems!.Sum(i => i.LineTotal);
        var sequence = await NextOrderSequenceAsync(db);
        var now = DateTime.UtcNow;

        var order = new Order
        {
            Id = orderId,
            Number = FormatOrderNumber(sequence),
            Sequence = sequence,
            Status = OrderStatus.Pendiente,
            PaymentStatus = PaymentStatus.Pendiente,
            CustomerId = customer.Id,
            ContactName = contactName,
            ContactPhone = contactPhone,
            DeliveryAddress = deliveryAddress,
            DeliveryZoneId = request.DeliveryZoneId,
            DeliveryFee = deliveryFee,
            PackagingOptionId = request.PackagingOptionId,
            PackagingCost = packagingCost,
            Subtotal = subtotal,
            Total = subtotal + deliveryFee,
            Notes = request.Notes,
            CreatedByAdminId = admin.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };
        order.Items = orderItems!;

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var saved = await LoadFullOrderAsync(db, order.Number);
        return TypedResults.Created($"/api/orders/{order.Number}", await ToDetailDtoAsync(saved!, db));
    }

    // "Editar pedido": an admin can add/remove products, fix the contact
    // details, or pick a delivery zone/packaging once they've followed up
    // with the customer over WhatsApp (a storefront order starts with none
    // of those set — see CreateStorefrontOrder). Full replace of the
    // editable fields, same shape as CreateOrderRequest minus
    // CreatedByAdminId (that's PUT .../admin's job) and Status/PaymentStatus
    // (their own controls). Blocked once the order is Entregado or
    // Cancelado — financial metrics already count a delivered order, and
    // editing its items would silently rewrite that history.
    private static async Task<Results<Ok<OrderDetailDto>, NotFound<string>, BadRequest<string>>> UpdateOrder(
        string number, UpdateOrderRequest request, IBravaDbContext db)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Number == number);
        if (order is null)
        {
            return TypedResults.NotFound($"Order '{number}' not found.");
        }
        if (order.Status is OrderStatus.Entregado or OrderStatus.Cancelado)
        {
            return TypedResults.BadRequest("No se puede editar un pedido entregado o cancelado.");
        }

        var contactName = request.ContactName.Trim();
        var contactPhone = request.ContactPhone.Trim();
        var deliveryAddress = request.DeliveryAddress.Trim();
        if (contactName.Length == 0 || contactPhone.Length == 0 || deliveryAddress.Length == 0)
        {
            return TypedResults.BadRequest("Nombre, teléfono y dirección son obligatorios.");
        }

        var itemsShapeError = ValidateItemRequests(request.Items);
        if (itemsShapeError is not null)
        {
            return TypedResults.BadRequest(itemsShapeError);
        }

        var (deliveryFee, zoneError) = await ResolveDeliveryFeeAsync(request.DeliveryZoneId, db);
        if (zoneError is not null)
        {
            return TypedResults.NotFound(zoneError);
        }

        var (packagingCost, packagingError) = await ResolvePackagingCostAsync(request.PackagingOptionId, db);
        if (packagingError is not null)
        {
            return TypedResults.NotFound(packagingError);
        }

        var (orderItems, itemsError) = await BuildOrderItemsAsync(request.Items, order.Id, db);
        if (itemsError is not null)
        {
            return TypedResults.BadRequest(itemsError);
        }

        order.ContactName = contactName;
        order.ContactPhone = contactPhone;
        order.DeliveryAddress = deliveryAddress;
        order.DeliveryZoneId = request.DeliveryZoneId;
        order.DeliveryFee = deliveryFee;
        order.PackagingOptionId = request.PackagingOptionId;
        order.PackagingCost = packagingCost;
        order.Notes = request.Notes;

        // Replace the lines wholesale. AddRange (not `order.Items = orderItems`)
        // so EF tracks them as Added regardless of key value — same pitfall
        // WishlistEndpoints.UpdateWishlist hit: assigning the navigation makes
        // DetectChanges treat a row with a pre-set (but never-inserted) Id as
        // Modified and emit a doomed UPDATE.
        db.OrderItems.RemoveRange(order.Items);
        db.OrderItems.AddRange(orderItems!);

        var subtotal = orderItems!.Sum(i => i.LineTotal);
        order.Subtotal = subtotal;
        order.Total = subtotal + deliveryFee;
        order.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var saved = await LoadFullOrderAsync(db, order.Number);
        return TypedResults.Ok(await ToDetailDtoAsync(saved!, db));
    }

    // The storefront's "Pedir por WhatsApp": the customer supplies their own
    // contact details and orders exactly what's in front of them (one
    // product/kit, or their whole wishlist). No admin, delivery zone or
    // packaging choice here — an admin fills those in from the panel once
    // they follow up over WhatsApp. Same Pendiente/Pendiente starting state
    // as an admin-entered order.
    private static async Task<Results<Created<CreateStorefrontOrderResponse>, BadRequest<string>>> CreateStorefrontOrder(
        CreateStorefrontOrderRequest request, IBravaDbContext db)
    {
        var contactName = (request.ContactName ?? string.Empty).Trim();
        var contactPhone = (request.ContactPhone ?? string.Empty).Trim();
        var deliveryAddress = (request.DeliveryAddress ?? string.Empty).Trim();
        if (contactName.Length == 0 || contactPhone.Length == 0 || deliveryAddress.Length == 0)
        {
            return TypedResults.BadRequest("Nombre, teléfono y dirección son obligatorios.");
        }
        if (contactName.Length > MaxContactFieldLength || contactPhone.Length > MaxContactFieldLength
            || deliveryAddress.Length > MaxContactFieldLength)
        {
            return TypedResults.BadRequest("Alguno de los datos de contacto es demasiado largo.");
        }

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (notes is { Length: > MaxNotesLength })
        {
            return TypedResults.BadRequest($"La nota no puede superar {MaxNotesLength} caracteres.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return TypedResults.BadRequest("El pedido necesita al menos un producto.");
        }
        if (request.Items.Count > MaxStorefrontItems)
        {
            return TypedResults.BadRequest($"El pedido no puede tener más de {MaxStorefrontItems} productos.");
        }
        var itemsShapeError = ValidateItemRequests(request.Items);
        if (itemsShapeError is not null)
        {
            return TypedResults.BadRequest(itemsShapeError);
        }

        var orderId = Guid.NewGuid();
        var (orderItems, itemsError) = await BuildOrderItemsAsync(request.Items, orderId, db);
        if (itemsError is not null)
        {
            return TypedResults.BadRequest(itemsError);
        }

        var customer = await FindOrCreateCustomerAsync(db, contactName, contactPhone);
        var subtotal = orderItems!.Sum(i => i.LineTotal);
        var sequence = await NextOrderSequenceAsync(db);
        var now = DateTime.UtcNow;

        var order = new Order
        {
            Id = orderId,
            Number = FormatOrderNumber(sequence),
            Sequence = sequence,
            Status = OrderStatus.Pendiente,
            PaymentStatus = PaymentStatus.Pendiente,
            CustomerId = customer.Id,
            ContactName = contactName,
            ContactPhone = contactPhone,
            DeliveryAddress = deliveryAddress,
            DeliveryZoneId = null,
            DeliveryFee = 0m,
            PackagingOptionId = null,
            PackagingCost = 0m,
            Subtotal = subtotal,
            Total = subtotal,
            Notes = notes,
            CreatedByAdminId = null,
            CreatedAt = now,
            UpdatedAt = now,
        };
        order.Items = orderItems!;

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return TypedResults.Created(
            $"/api/orders/{order.Number}", new CreateStorefrontOrderResponse(order.Number, order.Total));
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
        return TypedResults.Ok(await ToDetailDtoAsync(saved!, db));
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
        return TypedResults.Ok(await ToDetailDtoAsync(saved!, db));
    }

    // Lets an admin claim/reassign a customer-created order once they follow
    // up — the only way CreatedByAdminId gets set on one of those.
    private static async Task<Results<Ok<OrderDetailDto>, NotFound<string>>> AssignOrderAdmin(
        string number, AssignOrderAdminRequest request, IBravaDbContext db)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Number == number);
        if (order is null)
        {
            return TypedResults.NotFound($"Order '{number}' not found.");
        }

        var admin = await db.Admins.FirstOrDefaultAsync(a => a.Id == request.AdminId && a.IsActive);
        if (admin is null)
        {
            return TypedResults.NotFound($"Admin '{request.AdminId}' no encontrado o inactivo.");
        }

        order.CreatedByAdminId = admin.Id;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var saved = await LoadFullOrderAsync(db, order.Number);
        return TypedResults.Ok(await ToDetailDtoAsync(saved!, db));
    }

    private static Task<Order?> LoadFullOrderAsync(IBravaDbContext db, string number) =>
        db.Orders
            .Include(o => o.DeliveryZone)
            .Include(o => o.PackagingOption)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Number == number);

    // Order.CreatedByAdminId is id-only (no nav property — see the domain
    // comment), so the admin's email for display is a small separate lookup
    // rather than an Include. Skipped entirely when null (customer-created,
    // not yet claimed) rather than querying for a match that can't exist.
    private static async Task<OrderDetailDto> ToDetailDtoAsync(Order o, IBravaDbContext db)
    {
        var adminEmail = o.CreatedByAdminId is { } adminId
            ? await db.Admins.Where(a => a.Id == adminId).Select(a => a.Email).FirstOrDefaultAsync()
            : null;

        return new OrderDetailDto(
            o.Id, o.Number, o.Status, o.PaymentStatus, o.PaymentMethod, o.PaidAt, o.CustomerId,
            o.ContactName, o.ContactPhone, o.DeliveryAddress, o.DeliveryZoneId, o.DeliveryZone?.Name,
            o.DeliveryFee, o.PackagingOptionId, o.PackagingOption?.Name, o.PackagingCost,
            o.Subtotal, o.Total, o.Notes, o.CreatedAt,
            o.CreatedByAdminId, adminEmail,
            o.Items.Select(i => new OrderItemDetailDto(
                i.Id, i.ProductVariantId, i.ComboId, i.Description, i.UnitPrice, i.UnitCost, i.Quantity, i.LineTotal))
                .ToList());
    }

    // --- shared by CreateOrder, CreateStorefrontOrder and UpdateOrder --------

    private static async Task<(decimal Fee, string? Error)> ResolveDeliveryFeeAsync(Guid? zoneId, IBravaDbContext db)
    {
        if (zoneId is null)
        {
            return (0m, null);
        }
        var zone = await db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == zoneId);
        return zone is null ? (0m, $"Zona de envío '{zoneId}' no encontrada.") : (zone.Price, null);
    }

    private static async Task<(decimal Cost, string? Error)> ResolvePackagingCostAsync(Guid? packagingOptionId, IBravaDbContext db)
    {
        if (packagingOptionId is null)
        {
            return (0m, null);
        }
        var packaging = await db.PackagingOptions.FirstOrDefaultAsync(p => p.Id == packagingOptionId);
        return packaging is null ? (0m, $"Empaque '{packagingOptionId}' no encontrado.") : (packaging.Price, null);
    }

    private static string? ValidateItemRequests(List<CreateOrderItemRequest>? items)
    {
        if (items is null || items.Count == 0)
        {
            return "El pedido necesita al menos un producto.";
        }
        foreach (var item in items)
        {
            var hasVariant = item.ProductVariantId is not null;
            var hasCombo = item.ComboId is not null;
            if (hasVariant == hasCombo)
            {
                return "Cada línea debe tener exactamente un producto o un kit.";
            }
            if (item.Quantity < 1)
            {
                return "La cantidad debe ser al menos 1.";
            }
        }
        return null;
    }

    // Loads the referenced variants/combos, validates each is sellable, and
    // builds the snapshot OrderItems (description/price/cost) — the one place
    // both create endpoints price a line, so they can never disagree.
    private static async Task<(List<OrderItem>? Items, string? Error)> BuildOrderItemsAsync(
        List<CreateOrderItemRequest> requestItems, Guid orderId, IBravaDbContext db)
    {
        var variantIds = requestItems.Where(i => i.ProductVariantId is not null)
            .Select(i => i.ProductVariantId!.Value).Distinct().ToList();
        var comboIds = requestItems.Where(i => i.ComboId is not null)
            .Select(i => i.ComboId!.Value).Distinct().ToList();

        var variants = await db.ProductVariants
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id);
        var combos = await db.Combos
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant)
            .Where(c => comboIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        // Note: not `variantIds.FirstOrDefault(id => !variants.ContainsKey(id)) != Guid.Empty`
        // — FirstOrDefault's "nothing matched" sentinel IS Guid.Empty, so that
        // reads as "no missing variant" even when Guid.Empty is the actual
        // missing id (a garbage/anonymous request can easily send it), and
        // falls through to a dictionary lookup that throws instead of 400ing.
        var missingVariantId = variantIds.Where(id => !variants.ContainsKey(id)).Cast<Guid?>().FirstOrDefault();
        if (missingVariantId is not null)
        {
            return (null, $"Producto '{missingVariantId}' no encontrado.");
        }
        var missingComboId = comboIds.Where(id => !combos.ContainsKey(id)).Cast<Guid?>().FirstOrDefault();
        if (missingComboId is not null)
        {
            return (null, $"Kit '{missingComboId}' no encontrado.");
        }

        var orderItems = new List<OrderItem>();
        foreach (var item in requestItems)
        {
            if (item.ProductVariantId is { } variantId)
            {
                var variant = variants[variantId];
                var error = VariantPricingError(variant);
                if (error is not null)
                {
                    return (null, error);
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
                    return (null, error);
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

        return (orderItems, null);
    }

    private static async Task<Customer> FindOrCreateCustomerAsync(IBravaDbContext db, string name, string phone)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
        if (customer is not null)
        {
            return customer;
        }

        var now = DateTime.UtcNow;
        customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = name,
            Phone = phone,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Customers.Add(customer);
        return customer;
    }

    private static async Task<int> NextOrderSequenceAsync(IBravaDbContext db)
    {
        var maxSequence = await db.Orders.MaxAsync(o => (int?)o.Sequence) ?? 0;
        return maxSequence + 1;
    }

    private static string FormatOrderNumber(int sequence) => $"BRA-{sequence:D4}";

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
