using Brava.Domain.Brands;
using Brava.Domain.Categories;

namespace Brava.Domain.Products;

public class Product
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public required Guid BrandId { get; set; }

    public required Guid CategoryId { get; set; }

    public required string Slug { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// Manually admin-flagged "new arrival" — not derived from CreatedAt, so a
    /// restocked or re-promoted older product can be flagged without it
    /// actually being new, and the landing page's "Novedades" rail is curated
    /// rather than a rolling recency window.
    /// </summary>
    public bool IsFeatured { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Brand Brand { get; set; } = null!;

    public Category Category { get; set; } = null!;

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}