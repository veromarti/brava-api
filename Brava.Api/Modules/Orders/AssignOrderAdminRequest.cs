namespace Brava.Api.Modules.Orders;

/// <summary>
/// Claims/reassigns a customer-created order ("Cliente (WhatsApp)" in the
/// panel) to one of the admins — the storefront never sets CreatedByAdminId,
/// so this is the only way one gets attached after the fact.
/// </summary>
public record AssignOrderAdminRequest(Guid AdminId);
