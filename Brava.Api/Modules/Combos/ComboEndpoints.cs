using Brava.Api.Modules.Products.Images;
using Brava.Application;
using Brava.Domain;
using Brava.Domain.Combos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Combos;

public static class ComboEndpoints
{
    public static IEndpointRouteBuilder MapComboEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/combos", GetCombos);
        app.MapGet("/api/combos/admin", GetCombosForAdmin).RequireAuthorization();
        app.MapGet("/api/combos/{slug}", GetComboBySlug);
        app.MapGet("/api/combos/{slug}/admin", GetComboForAdmin).RequireAuthorization();
        app.MapPost("/api/combos", CreateCombo).RequireAuthorization();
        app.MapPut("/api/combos/{slug}", UpdateCombo).RequireAuthorization();
        app.MapDelete("/api/combos/{slug}", DeactivateCombo).RequireAuthorization();

        // A kit has a single image slot (not a gallery like a product), so
        // these set/clear one key on the combo rather than managing rows.
        app.MapPost("/api/combos/{slug}/image", UploadComboImage)
            .RequireAuthorization()
            .DisableAntiforgery();
        app.MapPost("/api/combos/{slug}/image/link", LinkComboImage)
            .RequireAuthorization();
        app.MapDelete("/api/combos/{slug}/image", DeleteComboImage)
            .RequireAuthorization();
        return app;
    }

    // Everything here materializes the full graph (Include) and computes
    // prices/labels in C# rather than trying to get EF to translate nested
    // aggregates + IImageStorage.GetPublicUrl (not SQL-translatable) into one
    // query. Combo/item counts are tiny (tens of rows), so this costs nothing
    // real — same tradeoff GetProducts/GetProductBySlug already make for images.
    private static async Task<Ok<List<ComboListItemDto>>> GetCombos(IBravaDbContext db, IImageStorage imageStorage)
    {
        var combos = await db.Combos
            .Where(c => c.IsActive)
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v.Product).ThenInclude(p => p.Images)
            .ToListAsync();

        var dtos = combos
            .Where(c => c.Items.Count > 0)
            .Select(c => new ComboListItemDto(c.Slug, c.Name, OriginalPrice(c), FinalPrice(c), ImageUrl(c, imageStorage)))
            .ToList();

        return TypedResults.Ok(dtos);
    }

    private static async Task<Ok<List<AdminComboListItemDto>>> GetCombosForAdmin(IBravaDbContext db)
    {
        var combos = await db.Combos
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var dtos = combos
            .Select(c => new AdminComboListItemDto(
                c.Id, c.Slug, c.Name, c.IsActive, OriginalPrice(c), c.ManualPrice, FinalPrice(c), c.Items.Count))
            .ToList();

        return TypedResults.Ok(dtos);
    }

    private static async Task<Results<Ok<ComboDetailDto>, NotFound>> GetComboBySlug(
        string slug, IBravaDbContext db, IImageStorage imageStorage)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var combo = await LoadFullComboAsync(db, normalizedSlug);
        if (combo is null)
        {
            return TypedResults.NotFound();
        }

        var dto = new ComboDetailDto(
            combo.Id, combo.Slug, combo.Name, combo.Description, combo.IsActive,
            OriginalPrice(combo), FinalPrice(combo), ImageUrl(combo, imageStorage), ItemDtos(combo));
        return TypedResults.Ok(dto);
    }

    private static async Task<Results<Ok<AdminComboDetailDto>, NotFound>> GetComboForAdmin(
        string slug, IBravaDbContext db, IImageStorage imageStorage)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var combo = await LoadFullComboAsync(db, normalizedSlug);
        if (combo is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToAdminDto(combo, imageStorage));
    }

    // Written directly under the same time pressure as the rest of this
    // admin surface (flagged for review like everything else this session):
    // a combo needs >= 1 variant, slug is server-generated like products,
    // and VariantIds are validated to exist before insert.
    private static async Task<Results<Created<AdminComboDetailDto>, NotFound<string>, BadRequest<string>>> CreateCombo(
        CreateComboRequest request, IBravaDbContext db, IImageStorage imageStorage)
    {
        if (request.VariantIds.Count == 0)
        {
            return TypedResults.BadRequest("A combo needs at least one variant.");
        }

        var existingCount = await db.ProductVariants.CountAsync(v => request.VariantIds.Contains(v.Id));
        if (existingCount != request.VariantIds.Distinct().Count())
        {
            return TypedResults.NotFound("One or more variant IDs don't exist.");
        }

        var baseSlug = SlugGenerator.Generate(request.Name);
        var slug = baseSlug;
        for (var suffix = 2; await db.Combos.AnyAsync(c => c.Slug == slug); suffix++)
        {
            slug = $"{baseSlug}-{suffix}";
        }

        // Id set explicitly (not left to the value generator) so ComboItems
        // can reference it in the same SaveChangesAsync batch below.
        var combo = new Combo
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Slug = slug,
            ManualPrice = request.ManualPrice,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Combos.Add(combo);

        foreach (var variantId in request.VariantIds)
        {
            db.ComboItems.Add(new ComboItem { ComboId = combo.Id, ProductVariantId = variantId });
        }

        await db.SaveChangesAsync();

        var saved = await LoadFullComboAsync(db, combo.Slug);
        return TypedResults.Created($"/api/combos/{combo.Slug}", ToAdminDto(saved!, imageStorage));
    }

    private static async Task<Results<Ok<AdminComboDetailDto>, NotFound<string>, BadRequest<string>>> UpdateCombo(
        string slug, UpdateComboRequest request, IBravaDbContext db, IImageStorage imageStorage)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var combo = await db.Combos.Include(c => c.Items).FirstOrDefaultAsync(c => c.Slug == normalizedSlug);
        if (combo is null)
        {
            return TypedResults.NotFound($"Combo '{slug}' not found.");
        }

        if (request.VariantIds.Count == 0)
        {
            return TypedResults.BadRequest("A combo needs at least one variant.");
        }

        var existingCount = await db.ProductVariants.CountAsync(v => request.VariantIds.Contains(v.Id));
        if (existingCount != request.VariantIds.Distinct().Count())
        {
            return TypedResults.NotFound($"One or more variant IDs don't exist.");
        }

        combo.Name = request.Name;
        combo.Description = request.Description;
        combo.ManualPrice = request.ManualPrice;
        combo.IsActive = request.IsActive;
        combo.UpdatedAt = DateTime.UtcNow;

        // Full replace of the item list, same as UpdateVariant does for a
        // variant's fields — simpler than diffing old vs new item sets.
        foreach (var item in combo.Items.ToList())
        {
            db.ComboItems.Remove(item);
        }
        foreach (var variantId in request.VariantIds)
        {
            db.ComboItems.Add(new ComboItem { ComboId = combo.Id, ProductVariantId = variantId });
        }

        await db.SaveChangesAsync();

        var saved = await LoadFullComboAsync(db, combo.Slug);
        return TypedResults.Ok(ToAdminDto(saved!, imageStorage));
    }

    // Soft delete, same reasoning as ProductEndpoints.DeactivateProduct.
    private static async Task<Results<NoContent, NotFound<string>>> DeactivateCombo(string slug, IBravaDbContext db)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var combo = await db.Combos.FirstOrDefaultAsync(c => c.Slug == normalizedSlug);
        if (combo is null)
        {
            return TypedResults.NotFound($"Combo '{slug}' not found.");
        }

        combo.IsActive = false;
        combo.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    private static Task<Combo?> LoadFullComboAsync(IBravaDbContext db, string normalizedSlug) =>
        db.Combos
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v.Product).ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(c => c.Slug == normalizedSlug);

    private static decimal OriginalPrice(Combo combo) => combo.Items.Sum(i => i.ProductVariant.SellPrice ?? 0);

    private static decimal FinalPrice(Combo combo) => combo.ManualPrice ?? OriginalPrice(combo);

    // The kit's own photo when one's been set; otherwise fall back to the
    // first member product's image (first item by Id — this codebase's Guids
    // are generated sequentially, so lowest Id approximates "added first").
    private static string? ImageUrl(Combo combo, IImageStorage imageStorage)
    {
        if (combo.ImageStorageKey is not null)
        {
            return imageStorage.GetPublicUrl(combo.ImageStorageKey);
        }

        var firstImage = combo.Items
            .OrderBy(i => i.Id)
            .SelectMany(i => i.ProductVariant.Product.Images.OrderBy(img => img.DisplayOrder))
            .FirstOrDefault();
        return firstImage is null ? null : imageStorage.GetPublicUrl(firstImage.StorageKey);
    }

    // Only a key we uploaded for this kit (under the combos/ prefix) is safe
    // to hard-delete from the bucket when the image is replaced or removed —
    // a linked key might be shared with a product image.
    private static bool IsOwnedComboKey(string? key) => key is not null && key.StartsWith("combos/");

    private static async Task<Results<Ok<ComboImageDto>, NotFound<string>, BadRequest<string>>> UploadComboImage(
        string slug, [FromForm] UploadComboImageRequest request, IBravaDbContext db, IImageStorage storage)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var combo = await db.Combos.FirstOrDefaultAsync(c => c.Slug == normalizedSlug);
        if (combo is null)
        {
            return TypedResults.NotFound($"Combo '{slug}' not found.");
        }

        if (request.File is null || request.File.Length == 0)
        {
            return TypedResults.BadRequest("File is required.");
        }

        if (request.File.Length > ImageUploadRules.MaxFileSizeBytes)
        {
            return TypedResults.BadRequest($"File exceeds the {ImageUploadRules.MaxFileSizeBytes / 1024 / 1024} MB limit.");
        }

        if (!ImageUploadRules.ExtensionByContentType.TryGetValue(request.File.ContentType, out var extension))
        {
            return TypedResults.BadRequest("Only JPEG, PNG, and WebP images are allowed.");
        }

        var key = $"combos/{combo.Id}/{Guid.NewGuid()}{extension}";
        await using (var stream = request.File.OpenReadStream())
        {
            await storage.UploadAsync(key, stream, request.File.ContentType);
        }

        var previousKey = combo.ImageStorageKey;
        combo.ImageStorageKey = key;
        combo.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (IsOwnedComboKey(previousKey) && previousKey != key)
        {
            await storage.DeleteAsync(previousKey!);
        }

        return TypedResults.Ok(new ComboImageDto(storage.GetPublicUrl(key)));
    }

    // For an image already sitting in the bucket (uploaded straight through
    // the Cloudflare dashboard). The URL must resolve back to a key in our
    // own bucket — same rule as the product image link endpoint.
    private static async Task<Results<Ok<ComboImageDto>, NotFound<string>, BadRequest<string>>> LinkComboImage(
        string slug, LinkComboImageRequest request, IBravaDbContext db, IImageStorage storage)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var combo = await db.Combos.FirstOrDefaultAsync(c => c.Slug == normalizedSlug);
        if (combo is null)
        {
            return TypedResults.NotFound($"Combo '{slug}' not found.");
        }

        if (!storage.TryGetKeyFromUrl(request.Url, out var key))
        {
            return TypedResults.BadRequest("URL must point to an object in this project's own R2 bucket.");
        }

        var previousKey = combo.ImageStorageKey;
        combo.ImageStorageKey = key;
        combo.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (IsOwnedComboKey(previousKey) && previousKey != key)
        {
            await storage.DeleteAsync(previousKey!);
        }

        return TypedResults.Ok(new ComboImageDto(storage.GetPublicUrl(key)));
    }

    private static async Task<Results<NoContent, NotFound<string>>> DeleteComboImage(
        string slug, IBravaDbContext db, IImageStorage storage)
    {
        var normalizedSlug = slug.ToLowerInvariant();
        var combo = await db.Combos.FirstOrDefaultAsync(c => c.Slug == normalizedSlug);
        if (combo is null)
        {
            return TypedResults.NotFound($"Combo '{slug}' not found.");
        }

        var previousKey = combo.ImageStorageKey;
        if (previousKey is not null)
        {
            combo.ImageStorageKey = null;
            combo.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            if (IsOwnedComboKey(previousKey))
            {
                await storage.DeleteAsync(previousKey);
            }
        }

        return TypedResults.NoContent();
    }

    private static List<ComboItemDetailDto> ItemDtos(Combo combo) =>
        combo.Items
            .Select(i => new ComboItemDetailDto(
                i.ProductVariantId,
                i.ProductVariant.Product.Slug,
                i.ProductVariant.Product.Name,
                i.ProductVariant.ToneCode,
                i.ProductVariant.ToneName,
                i.ProductVariant.Units,
                i.ProductVariant.VolumeMl,
                i.ProductVariant.MassG,
                i.ProductVariant.SellPrice ?? 0))
            .ToList();

    private static AdminComboDetailDto ToAdminDto(Combo combo, IImageStorage imageStorage) =>
        new(combo.Id, combo.Slug, combo.Name, combo.Description, combo.IsActive,
            OriginalPrice(combo), combo.ManualPrice, FinalPrice(combo),
            ImageUrl(combo, imageStorage), combo.ImageStorageKey is not null, ItemDtos(combo));
}
