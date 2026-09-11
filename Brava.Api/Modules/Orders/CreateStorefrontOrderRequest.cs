namespace Brava.Api.Modules.Orders;

// Anonymous, storefront-facing — "Pedir por WhatsApp" backed by a small
// contact form (name/phone/address). No DeliveryZoneId, PackagingOptionId or
// CreatedByAdminId: those are admin-only concerns an admin fills in from the
// panel once they follow up, same as any order that starts as a bare
// WhatsApp chat today.
public record CreateStorefrontOrderRequest(
    string ContactName,
    string ContactPhone,
    string DeliveryAddress,
    List<CreateOrderItemRequest> Items,
    string? Notes);

/// <summary>Just enough for the storefront to build its "Pedir por WhatsApp" message.</summary>
public record CreateStorefrontOrderResponse(string Number, decimal Total);
