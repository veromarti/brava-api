namespace Brava.Api.Modules.Orders;

/// <summary>
/// Full edit of an order's editable fields — same shape as
/// <see cref="CreateOrderRequest"/> minus CreatedByAdminId (PUT .../admin's
/// job) and Status/PaymentStatus (their own controls). Backs "Editar
/// pedido": an admin adding/removing products, fixing the address, or
/// picking a delivery zone/packaging once they've followed up with the
/// customer over WhatsApp.
/// </summary>
public record UpdateOrderRequest(
    string ContactName,
    string ContactPhone,
    string DeliveryAddress,
    Guid? DeliveryZoneId,
    Guid? PackagingOptionId,
    List<CreateOrderItemRequest> Items,
    string? Notes);
