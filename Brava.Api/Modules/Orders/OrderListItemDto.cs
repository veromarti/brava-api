using Brava.Domain.Orders;

namespace Brava.Api.Modules.Orders;

public record OrderListItemDto(
    Guid Id,
    string Number,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    string ContactName,
    string ContactPhone,
    decimal Total,
    DateTime CreatedAt);
