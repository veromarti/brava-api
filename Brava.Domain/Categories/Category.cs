using Brava.Domain.Products;

namespace Brava.Domain.Categories;

public class Category
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Slug { get; set; }

    public bool IsActive { get; set; }
    
    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}