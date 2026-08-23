namespace Brava.Domain.Products;

public class ProductVariant
{
    public Guid Id { get; set; }

    public required Guid ProductId { get; set; }

    public string? Sku { get; set; }

    public string? ToneCode { get; set; }

    public string? ToneName { get; set; }

    public int? Units { get; set; }

    public decimal? VolumeMl { get; set; }

    public decimal? MassG { get; set; }

    public decimal? CostPrice { get; set; }

    public decimal? SellPrice { get; set; }

    public required int PhysicalStock { get; set; }

    public bool AvailableOnDemand { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Product Product { get; set; } = null!;
}