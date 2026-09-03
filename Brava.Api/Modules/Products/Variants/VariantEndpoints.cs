using Brava.Application;
using Brava.Domain.Products;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Products.Variants;

public static class VariantEndpoints
{
    public static IEndpointRouteBuilder MapVariantEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products/{slug}/variants", CreateVariant).RequireAuthorization();
        app.MapPut("/api/products/{slug}/variants/{variantId:guid}", UpdateVariant).RequireAuthorization();
        app.MapPost("/api/products/variants/bulk-stock", BulkUpdateStock).RequireAuthorization();
        app.MapDelete("/api/products/{slug}/variants/{variantId:guid}", DeactivateVariant).RequireAuthorization();
        app.MapDelete("/api/products/{slug}/variants/{variantId:guid}/permanent", DeleteVariantPermanently).RequireAuthorization();
        app.MapPost("/api/products/{slug}/variants/{variantId:guid}/activate", ActivateVariant).RequireAuthorization();
        return app;
    }

    // Design calls made here (written under time pressure, not reviewed by Vero
    // first — see conversation): a variant can't be IsActive=true without a
    // SellPrice, mirroring the CSV seeder's isConfirmed rule (ADR-0003). SKU
    // uniqueness is checked with a query up front rather than caught off the
    // DB unique-index violation, so a duplicate SKU is a clean 409 instead of
    // an unhandled 500.
    private static async Task<Results<Created<VariantDto>, NotFound<string>, BadRequest<string>, Conflict<string>>> CreateVariant(
        string slug, CreateVariantRequest request, IBravaDbContext db)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var product = await db.Products.FirstOrDefaultAsync(p => p.Slug == normalizedSlug);
        if (product is null)
        {
            return TypedResults.NotFound($"Product '{slug}' not found.");
        }

        var validationError = Validate(request.IsActive, request.SellPrice, request.PhysicalStock);
        if (validationError is not null)
        {
            return TypedResults.BadRequest(validationError);
        }

        if (request.Sku is not null && await db.ProductVariants.AnyAsync(v => v.Sku == request.Sku))
        {
            return TypedResults.Conflict($"SKU '{request.Sku}' already exists.");
        }

        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Sku = request.Sku,
            ToneCode = request.ToneCode,
            ToneName = request.ToneName,
            Units = request.Units,
            VolumeMl = request.VolumeMl,
            MassG = request.MassG,
            CostPrice = request.CostPrice,
            SellPrice = request.SellPrice,
            PhysicalStock = request.PhysicalStock,
            AvailableOnDemand = request.AvailableOnDemand,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/products/{product.Slug}/variants/{variant.Id}", ToDto(variant));
    }

    private static async Task<Results<Ok<VariantDto>, NotFound<string>, BadRequest<string>, Conflict<string>>> UpdateVariant(
        string slug, Guid variantId, UpdateVariantRequest request, IBravaDbContext db)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var variant = await db.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId && v.Product.Slug == normalizedSlug);
        if (variant is null)
        {
            return TypedResults.NotFound($"Variant '{variantId}' not found for product '{slug}'.");
        }

        var validationError = Validate(request.IsActive, request.SellPrice, request.PhysicalStock);
        if (validationError is not null)
        {
            return TypedResults.BadRequest(validationError);
        }

        if (request.Sku is not null &&
            await db.ProductVariants.AnyAsync(v => v.Sku == request.Sku && v.Id != variantId))
        {
            return TypedResults.Conflict($"SKU '{request.Sku}' already exists.");
        }

        variant.Sku = request.Sku;
        variant.ToneCode = request.ToneCode;
        variant.ToneName = request.ToneName;
        variant.Units = request.Units;
        variant.VolumeMl = request.VolumeMl;
        variant.MassG = request.MassG;
        variant.CostPrice = request.CostPrice;
        variant.SellPrice = request.SellPrice;
        variant.PhysicalStock = request.PhysicalStock;
        variant.AvailableOnDemand = request.AvailableOnDemand;
        variant.IsActive = request.IsActive;
        variant.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return TypedResults.Ok(ToDto(variant));
    }

    // For the 3 admins to correct stock in one pass (e.g. after a physical
    // count) without a variant-by-variant UI. Unknown variant IDs are reported
    // back rather than failing the whole batch, since a typo'd ID shouldn't
    // block the other 50 correct rows.
    private static async Task<Results<Ok<BulkStockUpdateResult>, BadRequest<string>>> BulkUpdateStock(
        BulkStockUpdateRequest request, IBravaDbContext db)
    {
        if (request.Items.Count == 0)
        {
            return TypedResults.BadRequest("Items cannot be empty.");
        }

        if (request.Items.Any(i => i.PhysicalStock < 0))
        {
            return TypedResults.BadRequest("PhysicalStock cannot be negative.");
        }

        var ids = request.Items.Select(i => i.VariantId).ToList();
        var variants = await db.ProductVariants.Where(v => ids.Contains(v.Id)).ToListAsync();
        var byId = variants.ToDictionary(v => v.Id);

        var notFound = new List<Guid>();
        foreach (var item in request.Items)
        {
            if (!byId.TryGetValue(item.VariantId, out var variant))
            {
                notFound.Add(item.VariantId);
                continue;
            }

            variant.PhysicalStock = item.PhysicalStock;
            variant.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return TypedResults.Ok(new BulkStockUpdateResult(variants.Count, notFound));
    }

    // Soft delete, same reasoning as ProductEndpoints.DeactivateProduct — sets
    // IsActive=false rather than removing the row. A quicker path than PUT for
    // "just take this tone/size off the site," which otherwise requires
    // resending the full variant payload.
    private static async Task<Results<NoContent, NotFound<string>>> DeactivateVariant(
        string slug, Guid variantId, IBravaDbContext db)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var variant = await db.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId && v.Product.Slug == normalizedSlug);
        if (variant is null)
        {
            return TypedResults.NotFound($"Variant '{variantId}' not found for product '{slug}'.");
        }

        variant.IsActive = false;
        variant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    // Hard delete — actually removes the row, unlike DeactivateVariant's soft
    // IsActive=false. For cleaning up variants created by mistake. Blocked with
    // a 409 when the variant is in a combo (ComboItem -> ProductVariant is
    // Restrict): the kit has to be edited or the variant just deactivated
    // instead. Images pinned to this variant fall back to product-general —
    // their FK is ON DELETE SET NULL, done explicitly here so the behaviour
    // doesn't depend on the DB cascade.
    private static async Task<Results<NoContent, NotFound<string>, Conflict<string>>> DeleteVariantPermanently(
        string slug, Guid variantId, IBravaDbContext db)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var variant = await db.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId && v.Product.Slug == normalizedSlug);
        if (variant is null)
        {
            return TypedResults.NotFound($"Variant '{variantId}' not found for product '{slug}'.");
        }

        var comboCount = await db.ComboItems.CountAsync(ci => ci.ProductVariantId == variantId);
        if (comboCount > 0)
        {
            return TypedResults.Conflict(
                $"This variant is part of {comboCount} kit(s). Remove it from those kits or deactivate it instead.");
        }

        var pinnedImages = await db.ProductImages.Where(i => i.ProductVariantId == variantId).ToListAsync();
        foreach (var image in pinnedImages)
        {
            image.ProductVariantId = null;
            image.UpdatedAt = DateTime.UtcNow;
        }

        db.ProductVariants.Remove(variant);
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    // Symmetric to DeactivateVariant — a toggle, not a PUT. Deliberately
    // doesn't take a request body: the admin UI only ever has the *public*
    // ProductVariantDto loaded (no CostPrice on it), so reusing UpdateVariant
    // here would mean resending fields the client doesn't actually have,
    // silently nulling CostPrice out from under an admin who just wanted to
    // flip one flag back on. This checks the variant's already-stored
    // SellPrice instead of asking the client to send one.
    private static async Task<Results<NoContent, NotFound<string>, BadRequest<string>>> ActivateVariant(
        string slug, Guid variantId, IBravaDbContext db)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var variant = await db.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId && v.Product.Slug == normalizedSlug);
        if (variant is null)
        {
            return TypedResults.NotFound($"Variant '{variantId}' not found for product '{slug}'.");
        }

        if (variant.SellPrice is null)
        {
            return TypedResults.BadRequest("This variant has no price set — add one via edit before activating.");
        }

        variant.IsActive = true;
        variant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    private static string? Validate(bool isActive, decimal? sellPrice, int physicalStock)
    {
        if (isActive && sellPrice is null)
        {
            return "A variant cannot be active without a sell price.";
        }

        if (physicalStock < 0)
        {
            return "PhysicalStock cannot be negative.";
        }

        return null;
    }

    private static VariantDto ToDto(ProductVariant v) => new(
        v.Id, v.ProductId, v.Sku, v.ToneCode, v.ToneName, v.Units, v.VolumeMl, v.MassG,
        v.CostPrice, v.SellPrice, v.PhysicalStock, v.AvailableOnDemand, v.IsActive);
}
