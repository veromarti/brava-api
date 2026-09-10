using Brava.Application;
using Brava.Domain.Wishlists;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Wishlists;

public static class WishlistEndpoints
{
    // Storefront-facing and unauthenticated: a shopper builds a gift list in
    // the browser and shares the link. No accounts (ADR-0005); the creating
    // browser remembers the code to update the list later, and anyone with the
    // link can view it. Same low-stakes threat model as the rest of the public
    // storefront — these caps just bound the blast radius of a bad/abusive
    // payload, nothing more.
    private const int MaxItems = 50;
    private const int MaxNameLength = 80;
    private const int MaxNoteLength = 500;
    private const int MaxItemFieldLength = 300;
    private const int MaxCodeAttempts = 5;

    public static IEndpointRouteBuilder MapWishlistEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/wishlists", CreateWishlist);
        app.MapGet("/api/wishlists/{code}", GetWishlist);
        app.MapPut("/api/wishlists/{code}", UpdateWishlist);
        return app;
    }

    private static async Task<Results<Created<CreateWishlistResponse>, BadRequest<string>>> CreateWishlist(
        SaveWishlistRequest request, IBravaDbContext db)
    {
        var header = ValidateHeader(request);
        if (header.Error is not null)
        {
            return TypedResults.BadRequest(header.Error);
        }

        var id = Guid.NewGuid();
        var (items, itemsError) = BuildItems(request.Items, id);
        if (itemsError is not null)
        {
            return TypedResults.BadRequest(itemsError);
        }

        // MAX(...)+1-style accepted race, same as ComboEndpoints' slug loop and
        // OrderEndpoints' sequence: a collision here is astronomically unlikely
        // and the unique index is the real backstop.
        string code;
        var attempt = 0;
        do
        {
            code = WishlistCode.Generate();
            attempt++;
        }
        while (attempt < MaxCodeAttempts && await db.Wishlists.AnyAsync(w => w.Code == code));

        var now = DateTime.UtcNow;
        db.Wishlists.Add(new Wishlist
        {
            Id = id,
            Code = code,
            OwnerName = header.OwnerName,
            Note = header.Note,
            Items = items,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/wishlists/{code}", new CreateWishlistResponse(code));
    }

    private static async Task<Results<Ok<WishlistDto>, NotFound<string>>> GetWishlist(string code, IBravaDbContext db)
    {
        var wishlist = await db.Wishlists
            .Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.Code == code);

        if (wishlist is null)
        {
            return TypedResults.NotFound($"Lista '{code}' no encontrada.");
        }

        return TypedResults.Ok(ToDto(wishlist));
    }

    // Full replace, like UpdateDeliveryZone/UpdatePackagingOption: the owner's
    // browser re-sends the whole list (new name, note, lines) after they add or
    // remove something. No owner check — possession of the code is the only
    // credential this feature has.
    private static async Task<Results<Ok<WishlistDto>, NotFound<string>, BadRequest<string>>> UpdateWishlist(
        string code, SaveWishlistRequest request, IBravaDbContext db)
    {
        var wishlist = await db.Wishlists
            .Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.Code == code);

        if (wishlist is null)
        {
            return TypedResults.NotFound($"Lista '{code}' no encontrada.");
        }

        var header = ValidateHeader(request);
        if (header.Error is not null)
        {
            return TypedResults.BadRequest(header.Error);
        }

        var (items, itemsError) = BuildItems(request.Items, wishlist.Id);
        if (itemsError is not null)
        {
            return TypedResults.BadRequest(itemsError);
        }

        wishlist.OwnerName = header.OwnerName;
        wishlist.Note = header.Note;
        wishlist.UpdatedAt = DateTime.UtcNow;
        // Replace the lines wholesale. AddRange (not `wishlist.Items = items`)
        // so EF tracks them as Added regardless of key value — assigning the
        // navigation makes DetectChanges treat rows with a pre-set Id as
        // Modified and emit a doomed UPDATE.
        db.WishlistItems.RemoveRange(wishlist.Items);
        db.WishlistItems.AddRange(items);
        await db.SaveChangesAsync();

        wishlist.Items = items; // for the response projection only
        return TypedResults.Ok(ToDto(wishlist));
    }

    private static (string OwnerName, string? Note, string? Error) ValidateHeader(SaveWishlistRequest request)
    {
        var ownerName = (request.OwnerName ?? string.Empty).Trim();
        if (ownerName.Length == 0)
        {
            return (ownerName, null, "El nombre es obligatorio.");
        }
        if (ownerName.Length > MaxNameLength)
        {
            return (ownerName, null, $"El nombre no puede superar {MaxNameLength} caracteres.");
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > MaxNoteLength })
        {
            return (ownerName, null, $"La nota no puede superar {MaxNoteLength} caracteres.");
        }

        return (ownerName, note, null);
    }

    private static (List<WishlistItem> Items, string? Error) BuildItems(List<WishlistItemPayload>? payloads, Guid wishlistId)
    {
        if (payloads is null || payloads.Count == 0)
        {
            return ([], "La lista necesita al menos un producto.");
        }
        if (payloads.Count > MaxItems)
        {
            return ([], $"La lista no puede tener más de {MaxItems} productos.");
        }

        var items = new List<WishlistItem>(payloads.Count);
        foreach (var p in payloads)
        {
            if (p.Type is not ("product" or "combo"))
            {
                return ([], "Tipo de producto inválido.");
            }

            var slug = (p.Slug ?? string.Empty).Trim();
            var name = (p.Name ?? string.Empty).Trim();
            if (slug.Length is 0 or > MaxItemFieldLength || name.Length is 0 or > MaxItemFieldLength)
            {
                return ([], "Cada producto necesita un identificador y un nombre válidos.");
            }
            if (p.Quantity is < 1 or > 999)
            {
                return ([], "La cantidad debe estar entre 1 y 999.");
            }
            if (p.UnitPrice < 0)
            {
                return ([], "El precio no puede ser negativo.");
            }

            var variantLabel = string.IsNullOrWhiteSpace(p.VariantLabel) ? null : p.VariantLabel.Trim();
            var imageUrl = string.IsNullOrWhiteSpace(p.ImageUrl) ? null : p.ImageUrl.Trim();
            if (variantLabel is { Length: > MaxItemFieldLength } || imageUrl is { Length: > MaxItemFieldLength })
            {
                return ([], "Datos del producto demasiado largos.");
            }

            items.Add(new WishlistItem
            {
                WishlistId = wishlistId,
                ItemType = p.Type,
                Slug = slug,
                // Only products carry a variant; ignore a stray id on a combo line.
                VariantId = p.Type == "product" ? p.VariantId : null,
                Quantity = p.Quantity,
                Name = name,
                VariantLabel = variantLabel,
                ImageUrl = imageUrl,
                UnitPrice = p.UnitPrice,
            });
        }

        return (items, null);
    }

    private static WishlistDto ToDto(Wishlist w) => new(
        w.Code,
        w.OwnerName,
        w.Note,
        w.CreatedAt,
        w.UpdatedAt,
        w.Items
            .Select(i => new WishlistItemDto(
                i.ItemType, i.Slug, i.VariantId, i.Name, i.VariantLabel, i.ImageUrl, i.UnitPrice, i.Quantity))
            .ToList());
}
