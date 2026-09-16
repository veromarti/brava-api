using System.Globalization;
using System.Text;
using Brava.Application;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Products;

// "Optimize cost price input": most variants are missing CostPrice (see the
// catalogue-health metrics). Exporting the whole catalogue to a CSV an admin
// can fill in offline (Excel/Sheets) and re-upload beats a variant-by-variant
// edit form for a bulk pass. Deliberately narrow, matching the confirmed
// design: Stock/CostPrice/SellPrice are the only columns the re-upload can
// change — Producto/Marca/Variante/SKU are read-only context (so a row is
// identifiable without decoding a bare GUID) and ignored on import; renaming
// still goes through the normal product edit form. Update-only: a row whose
// VariantId doesn't match an existing variant is reported as an error, not
// created — that still goes through "Nuevo producto".
public static class CatalogueImportExportEndpoints
{
    private static readonly string[] Headers =
        ["VariantId", "Producto", "Marca", "Variante", "SKU", "Stock", "PrecioCosto", "PrecioVenta"];

    private const long MaxUploadBytes = 5 * 1024 * 1024;
    private const int MaxRows = 5000;

    public static IEndpointRouteBuilder MapCatalogueImportExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/export", ExportCatalogue).RequireAuthorization();
        app.MapPost("/api/products/import", ImportCatalogue).RequireAuthorization().DisableAntiforgery();
        return app;
    }

    private static async Task<IResult> ExportCatalogue(IBravaDbContext db)
    {
        var variants = await db.ProductVariants
            .Include(v => v.Product).ThenInclude(p => p.Brand)
            .OrderBy(v => v.Product.Name).ThenBy(v => v.ToneName).ThenBy(v => v.Id)
            .Select(v => new
            {
                v.Id,
                ProductName = v.Product.Name,
                BrandName = v.Product.Brand.Name,
                v.ToneCode,
                v.ToneName,
                v.Units,
                v.VolumeMl,
                v.MassG,
                v.Sku,
                v.PhysicalStock,
                v.CostPrice,
                v.SellPrice,
            })
            .ToListAsync();

        var csv = new StringBuilder();
        csv.Append(CsvUtils.BuildRow(Headers)).Append('\n');
        foreach (var v in variants)
        {
            csv.Append(CsvUtils.BuildRow(
            [
                v.Id.ToString(),
                v.ProductName,
                v.BrandName,
                BuildVariantLabel(v.ToneName, v.ToneCode, v.Units, v.VolumeMl, v.MassG),
                v.Sku ?? "",
                v.PhysicalStock.ToString(CultureInfo.InvariantCulture),
                FormatDecimal(v.CostPrice),
                FormatDecimal(v.SellPrice),
            ])).Append('\n');
        }

        // A leading UTF-8 BOM so Excel on Windows (the realistic target here)
        // opens this as UTF-8 instead of guessing a legacy codepage and
        // mangling every accented product/brand/tone name.
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv.ToString());
        return TypedResults.File(bytes, "text/csv", $"catalogo-brava-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    private static async Task<Results<Ok<ImportCatalogueResult>, BadRequest<string>>> ImportCatalogue(
        [FromForm] ImportCatalogueRequest request, IBravaDbContext db)
    {
        // request itself (not just request.File) comes back null when the
        // multipart body has no matching form fields at all — an empty
        // upload, not just an empty file — so this has to null-check the
        // whole parameter, not only .File.
        if (request?.File is not { Length: > 0 } file)
        {
            return TypedResults.BadRequest("Selecciona un archivo CSV.");
        }
        if (file.Length > MaxUploadBytes)
        {
            return TypedResults.BadRequest($"El archivo no puede superar {MaxUploadBytes / 1024 / 1024} MB.");
        }

        string text;
        await using (var stream = file.OpenReadStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            text = await reader.ReadToEndAsync();
        }

        var rows = CsvUtils.Parse(text);
        if (rows.Count == 0)
        {
            return TypedResults.BadRequest("El archivo está vacío.");
        }

        var header = rows[0];
        int ColumnIndex(string name) =>
            header.FindIndex(h => string.Equals(h.Trim(), name, StringComparison.OrdinalIgnoreCase));

        var idCol = ColumnIndex("VariantId");
        var stockCol = ColumnIndex("Stock");
        var costCol = ColumnIndex("PrecioCosto");
        var sellCol = ColumnIndex("PrecioVenta");
        if (idCol < 0 || stockCol < 0 || costCol < 0 || sellCol < 0)
        {
            return TypedResults.BadRequest(
                "El archivo debe tener las columnas VariantId, Stock, PrecioCosto y PrecioVenta " +
                "(descarga la plantilla actual con \"Exportar catálogo\").");
        }

        var dataRows = rows.Skip(1).Where(r => r.Any(f => f.Trim().Length > 0)).ToList();
        if (dataRows.Count > MaxRows)
        {
            return TypedResults.BadRequest($"El archivo no puede tener más de {MaxRows} filas.");
        }

        var errors = new List<ImportCatalogueRowError>();
        var parsedUpdates = new List<(Guid VariantId, int Stock, decimal? Cost, decimal? Sell, int RowNumber)>();

        for (var i = 0; i < dataRows.Count; i++)
        {
            var row = dataRows[i];
            var rowNumber = i + 2; // 1-based, +1 to account for the header row
            string Field(int col) => col < row.Count ? row[col].Trim() : "";

            var idText = Field(idCol);
            if (!Guid.TryParse(idText, out var variantId))
            {
                errors.Add(new ImportCatalogueRowError(rowNumber, idText, "VariantId inválido o vacío."));
                continue;
            }

            if (!int.TryParse(Field(stockCol), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stock)
                || stock < 0)
            {
                errors.Add(new ImportCatalogueRowError(rowNumber, idText, "Stock debe ser un número entero mayor o igual a 0."));
                continue;
            }

            if (!TryParseOptionalDecimal(Field(costCol), out var cost))
            {
                errors.Add(new ImportCatalogueRowError(rowNumber, idText, "PrecioCosto debe ser un número o estar vacío."));
                continue;
            }
            if (cost is < 0)
            {
                errors.Add(new ImportCatalogueRowError(rowNumber, idText, "PrecioCosto no puede ser negativo."));
                continue;
            }

            if (!TryParseOptionalDecimal(Field(sellCol), out var sell))
            {
                errors.Add(new ImportCatalogueRowError(rowNumber, idText, "PrecioVenta debe ser un número o estar vacío."));
                continue;
            }
            if (sell is < 0)
            {
                errors.Add(new ImportCatalogueRowError(rowNumber, idText, "PrecioVenta no puede ser negativo."));
                continue;
            }

            parsedUpdates.Add((variantId, stock, cost, sell, rowNumber));
        }

        var ids = parsedUpdates.Select(u => u.VariantId).ToList();
        var variantsById = await db.ProductVariants
            .Where(v => ids.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id);

        var updatedCount = 0;
        var now = DateTime.UtcNow;
        foreach (var update in parsedUpdates)
        {
            if (!variantsById.TryGetValue(update.VariantId, out var variant))
            {
                errors.Add(new ImportCatalogueRowError(
                    update.RowNumber, update.VariantId.ToString(), "No existe una variante con ese VariantId."));
                continue;
            }

            // Same invariant UpdateVariant enforces (ADR-0003): an active
            // variant always needs a sell price. A bulk file is exactly the
            // kind of input that could clear one by mistake (a dragged
            // formula, a stray delete), so this is checked here too rather
            // than trusted from the sheet.
            if (variant.IsActive && update.Sell is null)
            {
                errors.Add(new ImportCatalogueRowError(
                    update.RowNumber, update.VariantId.ToString(),
                    "Esta variante está activa y necesita un PrecioVenta — no puede quedar vacío."));
                continue;
            }

            variant.PhysicalStock = update.Stock;
            variant.CostPrice = update.Cost;
            variant.SellPrice = update.Sell;
            variant.UpdatedAt = now;
            updatedCount++;
        }

        await db.SaveChangesAsync();
        return TypedResults.Ok(new ImportCatalogueResult(updatedCount, errors));
    }

    private static bool TryParseOptionalDecimal(string text, out decimal? value)
    {
        if (text.Length == 0)
        {
            value = null;
            return true;
        }
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }
        value = null;
        return false;
    }

    // Whole pesos in practice (CLAUDE.md), so "18000" beats "18000.00" for
    // an admin skimming the sheet — trailing zeros after the decimal point
    // are dropped, a genuine fractional value (rare, but the column isn't
    // rounded) is kept.
    private static string FormatDecimal(decimal? value) => value?.ToString("0.####", CultureInfo.InvariantCulture) ?? "";

    // Same composition as lib/format.ts's variantLabel() on the frontend,
    // minus the product-name prefix (that's its own "Producto" column here).
    private static string BuildVariantLabel(string? toneName, string? toneCode, int? units, decimal? volumeMl, decimal? massG)
    {
        var parts = new List<string>();
        if (toneName is not null)
        {
            parts.Add(toneCode is not null ? $"{toneName} ({toneCode})" : toneName);
        }
        else if (toneCode is not null)
        {
            parts.Add(toneCode);
        }
        if (volumeMl is not null) parts.Add($"{volumeMl} ml");
        if (massG is not null) parts.Add($"{massG} g");
        if (units is not null) parts.Add($"{units} unidades");

        return parts.Count > 0 ? string.Join(" · ", parts) : "Único";
    }
}
