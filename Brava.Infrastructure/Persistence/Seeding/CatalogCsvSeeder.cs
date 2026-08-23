using System.Globalization;
using Brava.Domain.Brands;
using Brava.Domain.Categories;
using Brava.Domain.Products;
using Brava.Domain;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Brava.Infrastructure.Persistence.Seeding;

/// <summary>
/// ADR-0006: seeds Postgres from /data/*.csv instead of EF Core HasData.
/// Idempotent and keyed on slug so re-running it after a spreadsheet edit
/// updates existing rows instead of duplicating them.
/// </summary>
public static class CatalogCsvSeeder
{
    public static async Task SeedAsync(BravaDbContext db, string dataDirectory, CancellationToken ct = default)
    {
        var productRows = ReadCsv<ProductCsvRow>(Path.Combine(dataDirectory, "brava_products.csv"));
        var variantRows = ReadCsv<VariantCsvRow>(Path.Combine(dataDirectory, "brava_variants.csv"));

        var brandsByName = await UpsertBrandsAsync(db, productRows.Select(p => p.Brand), ct);
        var categoriesByName = await UpsertCategoriesAsync(db, productRows.Select(p => p.Category), ct);

        var existingProducts = await db.Products.ToDictionaryAsync(p => p.Slug, ct);
        var existingVariants = await db.ProductVariants.ToListAsync(ct);

        foreach (var row in productRows)
        {
            // The needs_review flag means the source PDF's price was missing or
            // truncated. The CSV still carries a placeholder number for it, so a
            // null-price check alone wouldn't catch these — the flag is what
            // means "not confirmed, don't publish."
            var isConfirmed = !string.Equals(row.NeedsReview, "SI", StringComparison.OrdinalIgnoreCase);

            if (!existingProducts.TryGetValue(row.Slug, out var product))
            {
                product = new Product
                {
                    Slug = row.Slug,
                    Name = row.Name,
                    Description = row.Description,
                    BrandId = brandsByName[row.Brand].Id,
                    CategoryId = categoriesByName[row.Category].Id,
                };
                db.Products.Add(product);
                existingProducts[row.Slug] = product;
            }
            else
            {
                product.Name = row.Name;
                product.Description = row.Description;
                product.BrandId = brandsByName[row.Brand].Id;
                product.CategoryId = categoriesByName[row.Category].Id;
            }

            product.IsActive = isConfirmed;
            product.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        foreach (var group in variantRows.GroupBy(v => v.ProductSlug))
        {
            if (!existingProducts.TryGetValue(group.Key, out var product))
            {
                // A variant row referencing a product slug that doesn't exist in
                // brava_products.csv is a data problem in the source CSVs, not
                // something the seeder should silently skip.
                throw new InvalidOperationException(
                    $"brava_variants.csv references unknown product slug '{group.Key}'.");
            }

            foreach (var row in group)
            {
                var sellPrice = ParseDecimal(row.SellPriceCop);
                var isConfirmed = product.IsActive && sellPrice is not null;

                var existing = existingVariants.FirstOrDefault(v =>
                    v.ProductId == product.Id &&
                    v.ToneCode == NullIfEmpty(row.ToneCode) &&
                    v.ToneName == NullIfEmpty(row.ToneName) &&
                    v.Units == ParseInt(row.Units) &&
                    v.VolumeMl == ParseDecimal(row.VolumeMl) &&
                    v.MassG == ParseDecimal(row.MassG));

                if (existing is null)
                {
                    existing = new ProductVariant
                    {
                        ProductId = product.Id,
                        ToneCode = NullIfEmpty(row.ToneCode),
                        ToneName = NullIfEmpty(row.ToneName),
                        Units = ParseInt(row.Units),
                        VolumeMl = ParseDecimal(row.VolumeMl),
                        MassG = ParseDecimal(row.MassG),
                        PhysicalStock = ParseInt(row.Stock) ?? 0,
                    };
                    db.ProductVariants.Add(existing);
                    existingVariants.Add(existing);
                }

                existing.SellPrice = sellPrice;
                // unitary_price_cop in the CSV is a placeholder equal to
                // sell_price_cop, not a real cost — leave CostPrice null until
                // the business owners fill it in, per CLAUDE.md section 8.
                existing.PhysicalStock = ParseInt(row.Stock) ?? 0;
                existing.AvailableOnDemand = bool.Parse(row.AvailableOnDemand);
                existing.IsActive = isConfirmed;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<Dictionary<string, Brand>> UpsertBrandsAsync(
        BravaDbContext db, IEnumerable<string> names, CancellationToken ct)
    {
        var existing = await db.Brands.ToDictionaryAsync(b => b.Name, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (existing.ContainsKey(name))
            {
                continue;
            }

            var brand = new Brand { Name = name, Slug = SlugGenerator.Generate(name), IsActive = true };
            db.Brands.Add(brand);
            existing[name] = brand;
        }

        await db.SaveChangesAsync(ct);
        return existing;
    }

    private static async Task<Dictionary<string, Category>> UpsertCategoriesAsync(
        BravaDbContext db, IEnumerable<string> names, CancellationToken ct)
    {
        var existing = await db.Categories.ToDictionaryAsync(c => c.Name, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (existing.ContainsKey(name))
            {
                continue;
            }

            var category = new Category { Name = name, Slug = SlugGenerator.Generate(name), IsActive = true };
            db.Categories.Add(category);
            existing[name] = category;
        }

        await db.SaveChangesAsync(ct);
        return existing;
    }

    private static List<T> ReadCsv<T>(string path)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";" };
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, config);
        return csv.GetRecords<T>().ToList();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static int? ParseInt(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : int.Parse(value, CultureInfo.InvariantCulture);

    private static decimal? ParseDecimal(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : decimal.Parse(value, CultureInfo.InvariantCulture);
}
