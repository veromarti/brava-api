namespace Brava.Api.Modules.Products;

// No Slug field — deliberately immutable after creation. Re-slugging on
// rename would break inbound WhatsApp/Instagram links (ADR-0002 flags this
// exact tradeoff and defers a slug_history table; keeping slugs fixed avoids
// needing one for v1).
public record UpdateProductRequest(
    string Name,
    string Description,
    Guid BrandId,
    Guid CategoryId,
    bool IsActive);
