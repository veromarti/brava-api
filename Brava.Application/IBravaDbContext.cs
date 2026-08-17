using Brava.Domain.Brands;
using Brava.Domain.Categories;
using Brava.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Brava.Application;

/// <summary>
/// The one seam between Application and Infrastructure: Application depends on
/// this interface, Infrastructure's BravaDbContext implements it. Without it,
/// a plain-Services Application layer would have to reference Infrastructure's
/// concrete DbContext directly, which points the dependency arrow backwards.
/// </summary>
public interface IBravaDbContext
{
    DbSet<Brand> Brands { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<ProductImage> ProductImages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
