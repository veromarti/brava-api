using CsvHelper.Configuration.Attributes;

namespace Brava.Infrastructure.Persistence.Seeding;

public sealed class ProductCsvRow
{
    [Name("brand")] public string Brand { get; set; } = "";
    [Name("name")] public string Name { get; set; } = "";
    [Name("category")] public string Category { get; set; } = "";
    [Name("slug")] public string Slug { get; set; } = "";
    [Name("description")] public string Description { get; set; } = "";
    [Name("needs_review")] public string NeedsReview { get; set; } = "";
}

public sealed class VariantCsvRow
{
    [Name("product_slug")] public string ProductSlug { get; set; } = "";
    [Name("tone_code")] public string? ToneCode { get; set; }
    [Name("tone_name")] public string? ToneName { get; set; }
    [Name("units")] public string? Units { get; set; }
    [Name("volume_ml")] public string? VolumeMl { get; set; }
    [Name("mass_g")] public string? MassG { get; set; }
    [Name("sell_price")] public string? SellPriceCop { get; set; }
    [Name("physical_stock")] public string? Stock { get; set; }
    [Name("available_on_demand")] public string AvailableOnDemand { get; set; } = "FALSE";
}
