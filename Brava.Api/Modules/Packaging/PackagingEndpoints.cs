using Brava.Application;
using Brava.Domain.Packaging;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Packaging;

public static class PackagingEndpoints
{
    public static IEndpointRouteBuilder MapPackagingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/packaging-options", GetPackagingOptions).RequireAuthorization();
        app.MapPost("/api/packaging-options", CreatePackagingOption).RequireAuthorization();
        app.MapPut("/api/packaging-options/{id:guid}", UpdatePackagingOption).RequireAuthorization();
        return app;
    }

    private static async Task<Ok<List<PackagingOptionDto>>> GetPackagingOptions(IBravaDbContext db)
    {
        var options = await db.PackagingOptions
            .OrderBy(p => p.Name)
            .Select(p => new PackagingOptionDto(p.Id, p.Name, p.Price, p.IsActive))
            .ToListAsync();

        return TypedResults.Ok(options);
    }

    // Name is unique ("Bolsa", "Caja chica", …) — same 409-on-dup reasoning as
    // CreateDeliveryZone: two options with one name is a data-entry slip.
    private static async Task<Results<Created<PackagingOptionDto>, BadRequest<string>, Conflict<string>>> CreatePackagingOption(
        CreatePackagingOptionRequest request, IBravaDbContext db)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return TypedResults.BadRequest("El nombre del empaque es obligatorio.");
        }

        if (request.Price < 0)
        {
            return TypedResults.BadRequest("El precio no puede ser negativo.");
        }

        var normalized = name.ToLowerInvariant();
        if (await db.PackagingOptions.AnyAsync(p => p.Name.ToLower() == normalized))
        {
            return TypedResults.Conflict($"Ya existe un empaque llamado '{name}'.");
        }

        var option = new PackagingOption
        {
            Name = name,
            Price = request.Price,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.PackagingOptions.Add(option);
        await db.SaveChangesAsync();

        return TypedResults.Created(
            $"/api/packaging-options/{option.Id}",
            new PackagingOptionDto(option.Id, option.Name, option.Price, option.IsActive));
    }

    private static async Task<Results<Ok<PackagingOptionDto>, NotFound<string>, BadRequest<string>, Conflict<string>>> UpdatePackagingOption(
        Guid id, UpdatePackagingOptionRequest request, IBravaDbContext db)
    {
        var option = await db.PackagingOptions.FirstOrDefaultAsync(p => p.Id == id);
        if (option is null)
        {
            return TypedResults.NotFound($"Empaque '{id}' no encontrado.");
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return TypedResults.BadRequest("El nombre del empaque es obligatorio.");
        }

        if (request.Price < 0)
        {
            return TypedResults.BadRequest("El precio no puede ser negativo.");
        }

        var normalized = name.ToLowerInvariant();
        if (await db.PackagingOptions.AnyAsync(p => p.Id != id && p.Name.ToLower() == normalized))
        {
            return TypedResults.Conflict($"Ya existe un empaque llamado '{name}'.");
        }

        option.Name = name;
        option.Price = request.Price;
        option.IsActive = request.IsActive;
        option.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.Ok(new PackagingOptionDto(option.Id, option.Name, option.Price, option.IsActive));
    }
}
