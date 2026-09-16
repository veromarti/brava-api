namespace Brava.Api.Modules.Products;

public record ImportCatalogueRowError(int Row, string? VariantId, string Message);

public record ImportCatalogueResult(int UpdatedCount, List<ImportCatalogueRowError> Errors);
