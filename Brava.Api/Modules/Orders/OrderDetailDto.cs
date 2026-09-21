using Brava.Domain.Orders;

namespace Brava.Api.Modules.Orders;

public record OrderDetailDto(
    Guid Id,
    string Number,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    PaymentMethod? PaymentMethod,
    DateTime? PaidAt,
    Guid? CustomerId,
    string ContactName,
    string ContactPhone,
    string DeliveryAddress,
    Guid? DeliveryZoneId,
    string? DeliveryZoneName,
    decimal DeliveryFee,
    Guid? PackagingOptionId,
    string? PackagingOptionName,
    decimal PackagingCost,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    string? Notes,
    DateTime CreatedAt,
    // Seller is null when the customer created this order themselves from
    // the storefront and no one has assigned it yet (PUT
    // /api/orders/{number}/seller). CreatedByAdminId/CreatedByAdminEmail are
    // historical only — orders created before Seller existed have those set
    // instead; new orders never do (see Order.CreatedByAdminId's doc comment).
    string? Seller,
    Guid? CreatedByAdminId,
    string? CreatedByAdminEmail,
    List<OrderItemDetailDto> Items);

public record OrderItemDetailDto(
    Guid Id,
    Guid? ProductVariantId,
    Guid? ComboId,
    string Description,
    decimal UnitPrice,
    decimal? UnitCost,
    int Quantity,
    decimal LineTotal);
