using Brava.Application;
using Brava.Domain.Products;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Products.Images;

public static class ImageEndpoints
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedContentTypes = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products/{slug}/images", UploadImage)
            .RequireAuthorization()
            .DisableAntiforgery();
        app.MapDelete("/api/products/{slug}/images/{imageId:guid}", DeleteImage)
            .RequireAuthorization();
        return app;
    }

    private static async Task<Results<Created<ImageDto>, NotFound<string>, BadRequest<string>>> UploadImage(
        string slug, [FromForm] UploadImageRequest request, IBravaDbContext db, IImageStorage storage)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var product = await db.Products.FirstOrDefaultAsync(p => p.Slug == normalizedSlug);
        if (product is null)
        {
            return TypedResults.NotFound($"Product '{slug}' not found.");
        }

        if (request.ProductVariantId is not null &&
            !await db.ProductVariants.AnyAsync(v => v.Id == request.ProductVariantId && v.ProductId == product.Id))
        {
            return TypedResults.BadRequest($"Variant '{request.ProductVariantId}' does not belong to product '{slug}'.");
        }

        if (request.File is null || request.File.Length == 0)
        {
            return TypedResults.BadRequest("File is required.");
        }

        if (request.File.Length > MaxFileSizeBytes)
        {
            return TypedResults.BadRequest($"File exceeds the {MaxFileSizeBytes / 1024 / 1024} MB limit.");
        }

        if (!AllowedContentTypes.TryGetValue(request.File.ContentType, out var extension))
        {
            return TypedResults.BadRequest("Only JPEG, PNG, and WebP images are allowed.");
        }

        var key = $"products/{product.Id}/{Guid.NewGuid()}{extension}";
        await using (var stream = request.File.OpenReadStream())
        {
            await storage.UploadAsync(key, stream, request.File.ContentType);
        }

        var image = new ProductImage
        {
            ProductId = product.Id,
            ProductVariantId = request.ProductVariantId,
            StorageKey = key,
            AltText = request.AltText,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ProductImages.Add(image);
        await db.SaveChangesAsync();

        var dto = new ImageDto(image.Id, storage.GetPublicUrl(key), image.AltText, image.DisplayOrder, image.ProductVariantId);
        return TypedResults.Created($"/api/products/{product.Slug}/images/{image.Id}", dto);
    }

    private static async Task<Results<NoContent, NotFound<string>>> DeleteImage(
        string slug, Guid imageId, IBravaDbContext db, IImageStorage storage)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var image = await db.ProductImages
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.Id == imageId && i.Product.Slug == normalizedSlug);
        if (image is null)
        {
            return TypedResults.NotFound($"Image '{imageId}' not found for product '{slug}'.");
        }

        await storage.DeleteAsync(image.StorageKey);
        db.ProductImages.Remove(image);
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}
