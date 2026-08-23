namespace Brava.Api.Modules.Products.Images;

public record LinkImageRequest(string Url, string AltText, int DisplayOrder, Guid? ProductVariantId);
