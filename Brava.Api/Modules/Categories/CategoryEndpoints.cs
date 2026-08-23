using Brava.Application;
using Brava.Domain;
using Brava.Domain.Categories;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Categories;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories", GetCategories);
        app.MapPost("/api/categories", CreateCategory).RequireAuthorization();
        return app;
    }

    private static async Task<Ok<List<CategoryListItemDto>>> GetCategories(IBravaDbContext db)
    {
        var categories = await db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new CategoryListItemDto(c.Id, c.Slug, c.Name, c.DisplayOrder))
            .ToListAsync();

        return TypedResults.Ok(categories);
    }

    // Same reasoning as BrandEndpoints.CreateBrand: identity is by Name
    // (case-insensitive), matching CatalogCsvSeeder's UpsertCategoriesAsync.
    private static async Task<Results<Created<CategoryDto>, Conflict<string>>> CreateCategory(
        CreateCategoryRequest request, IBravaDbContext db)
    {
        // See BrandEndpoints.CreateBrand for why this is ToLower(), not
        // ToLowerInvariant() — EF translates the former to SQL, not the latter.
        var normalizedName = request.Name.ToLowerInvariant();
        if (await db.Categories.AnyAsync(c => c.Name.ToLower() == normalizedName))
        {
            return TypedResults.Conflict($"A category named '{request.Name}' already exists.");
        }

        var baseSlug = SlugGenerator.Generate(request.Name);
        var slug = baseSlug;
        for (var suffix = 2; await db.Categories.AnyAsync(c => c.Slug == slug); suffix++)
        {
            slug = $"{baseSlug}-{suffix}";
        }

        var category = new Category
        {
            Name = request.Name,
            Slug = slug,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        return TypedResults.Created(
            $"/api/categories/{category.Slug}",
            new CategoryDto(category.Id, category.Slug, category.Name, category.DisplayOrder, category.IsActive));
    }
}
