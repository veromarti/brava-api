namespace Brava.Api.Modules.Delivery;

public record CreateDeliveryZoneRequest(string Name, decimal Price);

/// <summary>Full replace of the editable fields — same shape choice as UpdateVariant.</summary>
public record UpdateDeliveryZoneRequest(string Name, decimal Price, bool IsActive);
