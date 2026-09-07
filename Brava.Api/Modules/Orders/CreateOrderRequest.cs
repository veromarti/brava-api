namespace Brava.Api.Modules.Orders;

// CreatedByAdminId is picked explicitly on the create form, not assumed to be
// whoever is logged in — one admin often enters an order a colleague took
// over WhatsApp, so the panel has to ask rather than infer it from the token.
public record CreateOrderRequest(
    string ContactName,
    string ContactPhone,
    string DeliveryAddress,
    Guid? DeliveryZoneId,
    Guid CreatedByAdminId,
    List<CreateOrderItemRequest> Items,
    string? Notes);

/// <summary>Exactly one of ProductVariantId / ComboId must be set — a kit is one line, not its members expanded.</summary>
public record CreateOrderItemRequest(Guid? ProductVariantId, Guid? ComboId, int Quantity);
