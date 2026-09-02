using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Brava.Api.Modules.Combos;

public class UploadComboImageRequest
{
    [FromForm]
    public IFormFile File { get; set; } = null!;
}
