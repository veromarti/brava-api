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
    decimal Subtotal,
    decimal Total,
    string? Notes,
    DateTime CreatedAt,
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
