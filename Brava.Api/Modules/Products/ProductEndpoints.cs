using Brava.Api.Modules.Products.Images;
using Brava.Api.Modules.Products.Variants;
using Brava.Application;
using Brava.Domain;
using Brava.Domain.Orders;
using Brava.Domain.Products;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Products;
public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products", GetProducts);
        app.MapGet("/api/products/admin", GetProductsForAdmin).RequireAuthorization();
        // Dedicated literal routes, not query params on GET /api/products:
        // that route already ignores params it doesn't recognize, so
        // ?isNew=true would 200 with the whole catalogue instead of 404ing
        // (confirmed by hitting it directly). A literal segment also beats
        // the {slug} route below on specificity regardless of registration
        // order, but it's placed here to read the same way admin/{slug} do.
        app.MapGet("/api/products/new", GetNewProducts);
        app.MapGet("/api/products/best-sellers", GetBestSellers);
        app.MapGet("/api/products/{slug}", GetProductBySlug);
        app.MapGet("/api/products/{slug}/admin", GetProductForAdmin).RequireAuthorization();
        app.MapPost("/api/products", CreateProduct).RequireAuthorization();
        app.MapPut("/api/products/{slug}", UpdateProduct).RequireAuthorization();
        app.MapDelete("/api/products/{slug}", DeactivateProduct).RequireAuthorization();
        app.MapPost("/api/products/{slug}/feature", FeatureProduct).RequireAuthorization();
        app.MapPost("/api/products/{slug}/unfeature", UnfeatureProduct).RequireAuthorization();
        return app;
    }

    // ADR-0005: brand and category browsing. brand/category are slugs (not
    // names or ids) — same reasoning as every other slug lookup in this API,
    // consistent and stable to link to from the frontend's filter URLs.
    private static async Task<Ok<List<ProductListItemDto>>> GetProducts(
        IBravaDbContext db, IImageStorage imageStorage, string? brand, string? category)
    {
        var normalizedBrand = brand?.ToLowerInvariant();
        var normalizedCategory = category?.ToLowerInvariant();

        var query = db.Products
            .Where(p => p.IsActive)
            .Where(p => normalizedBrand == null || p.Brand.Slug == normalizedBrand)
            .Where(p => normalizedCategory == null || p.Category.Slug == normalizedCategory);

        var items = await BuildListItemsAsync(query, imageStorage);
        return TypedResults.Ok(items.Select(i => i.Dto).ToList());
    }

    // Manually admin-flagged rail (Product.IsFeatured) — not a recency
    // window off CreatedAt, so "Novedades" stays curated rather than
    // whatever happened to import last.
    private static async Task<Ok<List<ProductListItemDto>>> GetNewProducts(
        IBravaDbContext db, IImageStorage imageStorage)
    {
        var query = db.Products.Where(p => p.IsActive && p.IsFeatured);
        var items = await BuildListItemsAsync(query, imageStorage);
        return TypedResults.Ok(items.Select(i => i.Dto).ToList());
    }

    // Ranked by units sold on Entregado orders — same "completed sale" rule
    // GetOrderMetrics uses. A combo sale counts toward each of its component
    // products too (via ComboItem), not just products sold standalone,
    // otherwise a best-selling kit's products would rank as if nobody
    // bought them even though every kit sale moved that stock.
    private static async Task<Ok<List<ProductListItemDto>>> GetBestSellers(
        IBravaDbContext db, IImageStorage imageStorage, int? take)
    {
        var limit = take is > 0 ? take.Value : 10;

        var directSales = await db.Orders
            .Where(o => o.Status == OrderStatus.Entregado)
            .SelectMany(o => o.Items)
            .Where(i => i.ProductVariantId != null)
            .GroupBy(i => i.ProductVariant!.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Sum(i => i.Quantity) })
            .ToListAsync();

        var comboSales = await db.Orders
            .Where(o => o.Status == OrderStatus.Entregado)
            .SelectMany(o => o.Items)
            .Where(i => i.ComboId != null)
            .SelectMany(i => i.Combo!.Items.Select(ci => new { ci.ProductVariant.ProductId, i.Quantity }))
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Sum(x => x.Quantity) })
            .ToListAsync();

        var rankedIds = directSales.Concat(comboSales)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Sum(x => x.Units) })
            .OrderByDescending(x => x.Units)
            .Select(x => x.ProductId)
            .Take(limit)
            .ToList();

        // Same sellable-listing rules as GetProducts (active, at least one
        // active priced variant) — a discontinued best-seller shouldn't
        // reappear on the storefront just because it sold well historically.
        var items = await BuildListItemsAsync(
            db.Products.Where(p => p.IsActive && rankedIds.Contains(p.Id)), imageStorage);
        var dtoById = items.ToDictionary(i => i.Id, i => i.Dto);

        var ordered = rankedIds.Where(dtoById.ContainsKey).Select(id => dtoById[id]).ToList();
        return TypedResults.Ok(ordered);
    }

    // Shared by GetProducts/GetNewProducts/GetBestSellers: same two-phase
    // pattern as GetProductBySlug (IImageStorage.GetPublicUrl isn't
    // SQL-translatable, so the raw StorageKey is pulled first and turned
    // into a URL after the query materializes). Id comes along so
    // GetBestSellers can re-sort by its own ranking instead of query order.
    private static async Task<List<(Guid Id, ProductListItemDto Dto)>> BuildListItemsAsync(
        IQueryable<Product> query, IImageStorage imageStorage)
    {
        var products = await query
            .Select(p => new
            {
                p.Id,
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
                InStock = p.Variants.Any(v => v.IsActive && v.PhysicalStock > 0),
                ImageKey = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.StorageKey).FirstOrDefault(),
            })
            // A product with zero qualifying variants has no price to show and
            // must not appear in the listing at all (ADR-0003 consequence).
            .Where(p => p.ActivePrices.Any())
            .ToListAsync();

        return products
            .Select(p => (p.Id, new ProductListItemDto(
                p.Slug,
                p.Name,
                p.BrandName,
                p.CategoryName,
                p.ActivePrices.Min(),
                p.ActivePrices.Max(),
                p.InStock,
                p.ImageKey is null ? null : imageStorage.GetPublicUrl(p.ImageKey))))
            .ToList();
    }

    // No IsActive filter, unlike GetProducts — admins need to find and
    // reactivate/edit deactivated products, which the public listing hides.
    private static async Task<Ok<List<AdminProductListItemDto>>> GetProductsForAdmin(IBravaDbContext db)
    {
        var products = await db.Products
            .OrderBy(p => p.Name)
            .Select(p => new AdminProductListItemDto(
                p.Id, p.Slug, p.Name, p.Brand.Name, p.Category.Name, p.IsActive, p.IsFeatured, p.Images.Count))
            .ToListAsync();

        return TypedResults.Ok(products);
    }

    // ADR-0002: lowercase the incoming slug in C#, then query on it — never
    // WHERE LOWER(slug) = @p, that defeats the unique index.
    private static async Task<Results<Ok<ProductDetailDto>, NotFound>> GetProductBySlug(
        string slug, IBravaDbContext db, IImageStorage imageStorage)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        // IImageStorage.GetPublicUrl is a C# string builder, not SQL-translatable,
        // so images are pulled as raw StorageKeys first and turned into URLs
        // after the query materializes.
        var product = await db.Products
            .Where(p => p.Slug == normalizedSlug)
            .Select(p => new
            {
                p.Id,
                p.Slug,
                p.Name,
                p.Description,
                BrandName = p.Brand.Name,
                CategoryName = p.Category.Name,
                p.IsActive,
                Variants = p.Variants
                    .Select(v => new ProductVariantDto(
                        v.Id,
                        v.Sku,
                        v.ToneCode,
                        v.ToneName,
                        v.Units,
                        v.VolumeMl,
                        v.MassG,
                        v.SellPrice,
                        v.PhysicalStock,
                        v.AvailableOnDemand,
                        v.IsActive))
                    .ToList(),
                Images = p.Images
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new { i.Id, i.StorageKey, i.AltText, i.DisplayOrder, i.ProductVariantId })
                    .ToList(),
            })
            .FirstOrDefaultAsync();

        if (product is null)
        {
            return TypedResults.NotFound();
        }

        var dto = new ProductDetailDto(
            product.Id,
            product.Slug,
            product.Name,
            product.Description,
            product.BrandName,
            product.CategoryName,
            product.IsActive,
            product.Variants,
            product.Images
                .Select(i => new ImageDto(i.Id, imageStorage.GetPublicUrl(i.StorageKey), i.AltText, i.DisplayOrder, i.ProductVariantId))
                .ToList());

        return TypedResults.Ok(dto);
    }

    // Admin twin of GetProductBySlug: no IsActive gate (there isn't one here
    // anyway), variants carry CostPrice via the admin VariantDto, and
    // BrandId/CategoryId come down so edit forms don't reverse-map from names.
    private static async Task<Results<Ok<AdminProductDetailDto>, NotFound>> GetProductForAdmin(
        string slug, IBravaDbContext db, IImageStorage imageStorage)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var product = await db.Products
            .Where(p => p.Slug == normalizedSlug)
            .Select(p => new
            {
                p.Id,
                p.Slug,
                p.Name,
                p.Description,
                BrandName = p.Brand.Name,
                CategoryName = p.Category.Name,
                p.BrandId,
                p.CategoryId,
                p.IsActive,
                p.IsFeatured,
                Variants = p.Variants
                    .Select(v => new VariantDto(
                        v.Id,
                        v.ProductId,
                        v.Sku,
                        v.ToneCode,
                        v.ToneName,
                        v.Units,
                        v.VolumeMl,
                        v.MassG,
                        v.CostPrice,
                        v.SellPrice,
                        v.PhysicalStock,
                        v.AvailableOnDemand,
                        v.IsActive))
                    .ToList(),
                Images = p.Images
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new { i.Id, i.StorageKey, i.AltText, i.DisplayOrder, i.ProductVariantId })
                    .ToList(),
            })
            .FirstOrDefaultAsync();

        if (product is null)
        {
            return TypedResults.NotFound();
        }

        var dto = new AdminProductDetailDto(
            product.Id,
            product.Slug,
            product.Name,
            product.Description,
            product.BrandName,
            product.CategoryName,
            product.BrandId,
            product.CategoryId,
            product.IsActive,
            product.IsFeatured,
            product.Variants,
            product.Images
                .Select(i => new ImageDto(i.Id, imageStorage.GetPublicUrl(i.StorageKey), i.AltText, i.DisplayOrder, i.ProductVariantId))
                .ToList());

        return TypedResults.Ok(dto);
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

    // Slug is immutable — see UpdateProductRequest's comment. BrandId/CategoryId
    // are re-validated the same way CreateProduct validates them; a full
    // replace, like UpdateVariant, not a patch.
    private static async Task<Results<Ok<ProductDto>, NotFound<string>>> UpdateProduct(
        string slug, UpdateProductRequest request, IBravaDbContext db)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var product = await db.Products.FirstOrDefaultAsync(p => p.Slug == normalizedSlug);
        if (product is null)
        {
            return TypedResults.NotFound($"Product '{slug}' not found.");
        }

        if (!await db.Brands.AnyAsync(b => b.Id == request.BrandId))
        {
            return TypedResults.NotFound($"Brand '{request.BrandId}' not found.");
        }

        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId))
        {
            return TypedResults.NotFound($"Category '{request.CategoryId}' not found.");
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.BrandId = request.BrandId;
        product.CategoryId = request.CategoryId;
        product.IsActive = request.IsActive;
        product.IsFeatured = request.IsFeatured;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var dto = new ProductDto(product.Id, product.Slug, product.Name, product.Description, product.IsActive);
        return TypedResults.Ok(dto);
    }

    // Soft delete, not a row removal — sets IsActive=false so the product
    // drops out of GET /api/products (which already filters on IsActive) and
    // GET /api/products/{slug} still returns it for admins/audit, just with
    // IsActive=false. No hard DELETE exists; nothing in this codebase removes
    // catalog rows outright.
    private static async Task<Results<NoContent, NotFound<string>>> DeactivateProduct(
        string slug, IBravaDbContext db)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var product = await db.Products.FirstOrDefaultAsync(p => p.Slug == normalizedSlug);
        if (product is null)
        {
            return TypedResults.NotFound($"Product '{slug}' not found.");
        }

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    // Symmetric to VariantEndpoints.ActivateVariant — a toggle, not a PUT, so
    // the admin products list can flip "new" status with one lightweight
    // call instead of needing the full UpdateProduct payload (name,
    // description, brand, category) it doesn't have loaded.
    private static async Task<Results<NoContent, NotFound<string>>> FeatureProduct(string slug, IBravaDbContext db)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var product = await db.Products.FirstOrDefaultAsync(p => p.Slug == normalizedSlug);
        if (product is null)
        {
            return TypedResults.NotFound($"Product '{slug}' not found.");
        }

        product.IsFeatured = true;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound<string>>> UnfeatureProduct(string slug, IBravaDbContext db)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var product = await db.Products.FirstOrDefaultAsync(p => p.Slug == normalizedSlug);
        if (product is null)
        {
            return TypedResults.NotFound($"Product '{slug}' not found.");
        }

        product.IsFeatured = false;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}
