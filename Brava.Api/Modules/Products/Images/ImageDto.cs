namespace Brava.Api.Modules.Products.Images;

public record ImageDto(
    Guid Id,
    string Url,
    string AltText,
    int DisplayOrder,
    Guid? ProductVariantId);
