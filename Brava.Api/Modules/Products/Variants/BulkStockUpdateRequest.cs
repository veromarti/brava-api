namespace Brava.Api.Modules.Products.Variants;

public record BulkStockUpdateItem(Guid VariantId, int PhysicalStock);

public record BulkStockUpdateRequest(List<BulkStockUpdateItem> Items);

public record BulkStockUpdateResult(int UpdatedCount, List<Guid> NotFoundVariantIds);
