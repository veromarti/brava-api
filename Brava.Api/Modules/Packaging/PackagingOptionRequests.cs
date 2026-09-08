namespace Brava.Api.Modules.Packaging;

public record CreatePackagingOptionRequest(string Name, decimal Price);

/// <summary>Full replace of the editable fields — same shape choice as UpdateDeliveryZoneRequest.</summary>
public record UpdatePackagingOptionRequest(string Name, decimal Price, bool IsActive);
