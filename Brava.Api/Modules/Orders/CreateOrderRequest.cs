namespace Brava.Api.Modules.Orders;

public record CreateOrderRequest(
    string ContactName,
    string ContactPhone,
    string DeliveryAddress,
    Guid? DeliveryZoneId,
    List<CreateOrderItemRequest> Items,
    string? Notes);

/// <summary>Exactly one of ProductVariantId / ComboId must be set — a kit is one line, not its members expanded.</summary>
public record CreateOrderItemRequest(Guid? ProductVariantId, Guid? ComboId, int Quantity);
