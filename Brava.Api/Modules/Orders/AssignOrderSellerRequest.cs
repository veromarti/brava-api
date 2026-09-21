namespace Brava.Api.Modules.Orders;

/// <summary>
/// Assigns/reassigns a seller (one of OrderEndpoints.AllowedSellers) to an
/// order — the only way a customer-created order ("Cliente (WhatsApp)" in
/// the panel) gets one after the fact, since the storefront never sets it.
/// </summary>
public record AssignOrderSellerRequest(string Seller);
