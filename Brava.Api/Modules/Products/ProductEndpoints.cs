using Brava.Application;
using Brava.Domain;
using Brava.Domain.Products;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Products;
public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products", GetProducts);
        app.MapPost("/api/products", CreateProduct);
        return app;
    }

    private static async Task<Ok<List<ProductListItemDto>>> GetProducts(IBravaDbContext db)
    {
        var products = await db.Products
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.Slug,
                p.Name,
                BrandName = p.Brand.Name,
                CategoryName = p.Category.Name,
                // ADR-0003: only active variants with a confirmed price count
                // toward the listing price. needs_review rows import with
                // SellPrice null / IsActive false, so they're excluded here too.
                ActivePrices = p.Variants
                    .Where(v => v.IsActive && v.SellPrice != null)
                    .Select(v => v.SellPrice!.Value),
            })
            // A product with zero qualifying variants has no price to show and
            // must not appear in the listing at all (ADR-0003 consequence).
            .Where(p => p.ActivePrices.Any())
            .Select(p => new ProductListItemDto(
                p.Slug,
                p.Name,
                p.BrandName,
                p.CategoryName,
                p.ActivePrices.Min(),
                p.ActivePrices.Max()))
            .ToListAsync();

        return TypedResults.Ok(products);
    }

    // Decisions locked in: products may be created with zero variants (not all
    // products need one); slug is server-generated via SlugGenerator, and a
    // collision auto-suffixes (-2, -3, ...) rather than rejecting; BrandId and
    // CategoryId are validated to exist before insert.
    //
    // Still needed on your side: an ADR (amend ADR-0005 or add a new one)
    // recording that write endpoints start now instead of after v1 launch.
    private static async Task<Results<Created<ProductDto>, NotFound<string>>> CreateProduct(
        CreateProductRequest request, IBravaDbContext db)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(b => b.Id == request.BrandId);
        if (brand is null)
        {
            return TypedResults.NotFound($"Brand '{request.BrandId}' not found.");
        }

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId);
        if (category is null)
        {
            return TypedResults.NotFound($"Category '{request.CategoryId}' not found.");
        }

        var baseSlug = SlugGenerator.Generate(brand.Name, request.Name);
        var slug = baseSlug;
        for (var suffix = 2; await db.Products.AnyAsync(p => p.Slug == slug); suffix++)
        {
            slug = $"{baseSlug}-{suffix}";
        }

        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            BrandId = request.BrandId,
            CategoryId = request.CategoryId,
            Slug = slug,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var dto = new ProductDto(product.Id, product.Slug, product.Name, product.Description, product.IsActive);
        return TypedResults.Created($"/api/products/{product.Slug}", dto);
    }
}
