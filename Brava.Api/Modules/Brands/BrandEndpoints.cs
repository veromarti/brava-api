using Brava.Application;
using Brava.Domain;
using Brava.Domain.Brands;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Brands;
public static class BrandEndpoints
{
    public static IEndpointRouteBuilder MapBrandEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/brands", GetBrands);
        app.MapPost("/api/brands", CreateBrand).RequireAuthorization();
        return app;
    }

    private static async Task<Ok<List<BrandListItemDto>>> GetBrands(IBravaDbContext db)
    {
        var brands = await db.Brands
            .Where(b => b.IsActive)
            .Select(b => new BrandListItemDto(b.Id, b.Slug, b.Name))
            .ToListAsync();

        return TypedResults.Ok(brands);
    }

    // Brand identity is by Name (case-insensitive), matching CatalogCsvSeeder's
    // UpsertBrandsAsync — two brands with the same name is a data-entry
    // mistake, not a valid catalog state, so it's a 409 rather than silently
    // allowing a second "Ani-k" with a suffixed slug.
    private static async Task<Results<Created<BrandDto>, Conflict<string>>> CreateBrand(
        CreateBrandRequest request, IBravaDbContext db)
    {
        // ToLower(), not ToLowerInvariant() — this runs inside an EF Core LINQ
        // expression translated to SQL (Postgres LOWER()), not executed as CLR
        // code. ToLowerInvariant() has no SQL translation and throws at
        // runtime. CLAUDE.md's "always ToLowerInvariant()" rule is about C#
        // string comparisons; it doesn't apply once EF is compiling this to SQL.
        var normalizedName = request.Name.ToLowerInvariant();
        if (await db.Brands.AnyAsync(b => b.Name.ToLower() == normalizedName))
        {
            return TypedResults.Conflict($"A brand named '{request.Name}' already exists.");
        }

        var baseSlug = SlugGenerator.Generate(request.Name);
        var slug = baseSlug;
        for (var suffix = 2; await db.Brands.AnyAsync(b => b.Slug == slug); suffix++)
        {
            slug = $"{baseSlug}-{suffix}";
        }

        var brand = new Brand
        {
            Name = request.Name,
            Slug = slug,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Brands.Add(brand);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/brands/{brand.Slug}", new BrandDto(brand.Id, brand.Slug, brand.Name, brand.IsActive));
    }
}