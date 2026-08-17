using Brava.Application;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Brands;
public static class BrandEndpoints
{
    public static IEndpointRouteBuilder MapBrandEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/brands", GetBrands);
        return app;
    }

    private static async Task<Ok<List<BrandListItemDto>>> GetBrands(IBravaDbContext db)
    {
        var brands = await db.Brands
            .Where(b => b.IsActive)
            .Select(b => new BrandListItemDto(b.Slug, b.Name))
            .ToListAsync();

        return TypedResults.Ok(brands);
    }
}