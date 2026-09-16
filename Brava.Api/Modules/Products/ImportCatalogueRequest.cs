using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Brava.Api.Modules.Products;

public class ImportCatalogueRequest
{
    [FromForm]
    public IFormFile File { get; set; } = null!;
}
