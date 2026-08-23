using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Brava.Api.Modules.Products.Images;

public class UploadImageRequest
{
    [FromForm]
    public IFormFile File { get; set; } = null!;

    [FromForm]
    public string AltText { get; set; } = "";

    [FromForm]
    public int DisplayOrder { get; set; }

    [FromForm]
    public Guid? ProductVariantId { get; set; }
}
