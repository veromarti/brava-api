namespace Brava.Api.Modules.Products.Images;

// Shared by the product-image upload (ImageEndpoints) and the combo-image
// upload (ComboEndpoints) so the size cap and accepted content types stay
// in one place instead of drifting apart.
public static class ImageUploadRules
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public static readonly IReadOnlyDictionary<string, string> ExtensionByContentType =
        new Dictionary<string, string>
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
        };
}
