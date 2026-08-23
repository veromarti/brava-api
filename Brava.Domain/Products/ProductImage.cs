namespace Brava.Domain.Products;

public class ProductImage
{
    public Guid Id { get; set; }

    public required Guid ProductId { get; set; }

    public Guid? ProductVariantId { get; set; }

    public required string StorageKey { get; set; }

    public required string AltText { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Product Product { get; set; } = null!;

    public ProductVariant? ProductVariant { get; set; }
}