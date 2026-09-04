using Brava.Domain.Delivery;

namespace Brava.Infrastructure.Persistence.Seeding;

/// <summary>
/// Medellín's 16 comunas, seeded through the AddOrders migration so every
/// environment has them right after `ef database update` — no separate seeding
/// step, unlike the catalog CSV (that data changes; this list doesn't).
///
/// Prices start at 0 and are set by admins in the panel. These seed values are
/// never edited here again: changing one would make EF emit an UpdateData that
/// could clobber an admin's price. New zones (nearby municipalities,
/// corregimientos) are added through the panel, not here.
/// </summary>
public static class DeliveryZoneSeedData
{
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static readonly IReadOnlyList<DeliveryZone> Zones =
    [
        Zone("d1a00000-0000-4000-a000-000000000001", "Popular"),
        Zone("d1a00000-0000-4000-a000-000000000002", "Santa Cruz"),
        Zone("d1a00000-0000-4000-a000-000000000003", "Manrique"),
        Zone("d1a00000-0000-4000-a000-000000000004", "Aranjuez"),
        Zone("d1a00000-0000-4000-a000-000000000005", "Castilla"),
        Zone("d1a00000-0000-4000-a000-000000000006", "Doce de Octubre"),
        Zone("d1a00000-0000-4000-a000-000000000007", "Robledo"),
        Zone("d1a00000-0000-4000-a000-000000000008", "Villa Hermosa"),
        Zone("d1a00000-0000-4000-a000-000000000009", "Buenos Aires"),
        Zone("d1a00000-0000-4000-a000-00000000000a", "La Candelaria"),
        Zone("d1a00000-0000-4000-a000-00000000000b", "Laureles - Estadio"),
        Zone("d1a00000-0000-4000-a000-00000000000c", "La América"),
        Zone("d1a00000-0000-4000-a000-00000000000d", "San Javier"),
        Zone("d1a00000-0000-4000-a000-00000000000e", "El Poblado"),
        Zone("d1a00000-0000-4000-a000-00000000000f", "Guayabal"),
        Zone("d1a00000-0000-4000-a000-000000000010", "Belén"),
    ];

    private static DeliveryZone Zone(string id, string name) => new()
    {
        Id = new Guid(id),
        Name = name,
        Price = 0m,
        IsActive = true,
        CreatedAt = SeededAt,
        UpdatedAt = SeededAt,
    };
}
