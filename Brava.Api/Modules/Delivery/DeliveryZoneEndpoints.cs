using Brava.Application;
using Brava.Domain.Delivery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Delivery;

public static class DeliveryZoneEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryZoneEndpoints(this IEndpointRouteBuilder app)
    {
        // All admin-only for now. A public "active zones + prices" endpoint
        // comes with the storefront delivery-fee lookup (Phase 3).
        app.MapGet("/api/delivery-zones", GetDeliveryZones).RequireAuthorization();
        app.MapPost("/api/delivery-zones", CreateDeliveryZone).RequireAuthorization();
        app.MapPut("/api/delivery-zones/{id:guid}", UpdateDeliveryZone).RequireAuthorization();
        return app;
    }

    private static async Task<Ok<List<DeliveryZoneDto>>> GetDeliveryZones(IBravaDbContext db)
    {
        var zones = await db.DeliveryZones
            .OrderBy(z => z.Name)
            .Select(z => new DeliveryZoneDto(z.Id, z.Name, z.Price, z.IsActive))
            .ToListAsync();

        return TypedResults.Ok(zones);
    }

    // Name is unique (comuna, municipality, corregimiento…). Same 409-on-dup
    // reasoning as CreateBrand — two zones with one name is a data-entry slip.
    private static async Task<Results<Created<DeliveryZoneDto>, BadRequest<string>, Conflict<string>>> CreateDeliveryZone(
        CreateDeliveryZoneRequest request, IBravaDbContext db)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return TypedResults.BadRequest("El nombre de la zona es obligatorio.");
        }

        if (request.Price < 0)
        {
            return TypedResults.BadRequest("El precio no puede ser negativo.");
        }

        var normalized = name.ToLowerInvariant();
        if (await db.DeliveryZones.AnyAsync(z => z.Name.ToLower() == normalized))
        {
            return TypedResults.Conflict($"Ya existe una zona llamada '{name}'.");
        }

        var zone = new DeliveryZone
        {
            Name = name,
            Price = request.Price,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.DeliveryZones.Add(zone);
        await db.SaveChangesAsync();

        return TypedResults.Created(
            $"/api/delivery-zones/{zone.Id}",
            new DeliveryZoneDto(zone.Id, zone.Name, zone.Price, zone.IsActive));
    }

    private static async Task<Results<Ok<DeliveryZoneDto>, NotFound<string>, BadRequest<string>, Conflict<string>>> UpdateDeliveryZone(
        Guid id, UpdateDeliveryZoneRequest request, IBravaDbContext db)
    {
        var zone = await db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == id);
        if (zone is null)
        {
            return TypedResults.NotFound($"Zona '{id}' no encontrada.");
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return TypedResults.BadRequest("El nombre de la zona es obligatorio.");
        }

        if (request.Price < 0)
        {
            return TypedResults.BadRequest("El precio no puede ser negativo.");
        }

        var normalized = name.ToLowerInvariant();
        if (await db.DeliveryZones.AnyAsync(z => z.Id != id && z.Name.ToLower() == normalized))
        {
            return TypedResults.Conflict($"Ya existe una zona llamada '{name}'.");
        }

        zone.Name = name;
        zone.Price = request.Price;
        zone.IsActive = request.IsActive;
        zone.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.Ok(new DeliveryZoneDto(zone.Id, zone.Name, zone.Price, zone.IsActive));
    }
}
