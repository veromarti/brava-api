using Brava.Application;
using Brava.Domain.Admins;
using Brava.Domain.Brands;
using Brava.Domain.Categories;
using Brava.Domain.Combos;
using Brava.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Brava.Infrastructure.Persistence;

public class BravaDbContext(DbContextOptions<BravaDbContext> options) : DbContext(options), IBravaDbContext
{
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Combo> Combos => Set<Combo>();
    public DbSet<ComboItem> ComboItems => Set<ComboItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ADR-0002: slugs are the canonical lookup key and must be unique.
        // Categories are URL-addressable in v1, so they get the same rule.
        modelBuilder.Entity<Brand>().HasIndex(b => b.Slug).IsUnique();
        modelBuilder.Entity<Category>().HasIndex(c => c.Slug).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(p => p.Slug).IsUnique();
        modelBuilder.Entity<Admin>().HasIndex(a => a.Email).IsUnique();


        // Unique when present; Postgres allows multiple NULLs through a unique index.
        modelBuilder.Entity<ProductVariant>().HasIndex(v => v.Sku).IsUnique();

        // ADR-0003: price lives on the variant. Whole-peso money, not measurements.
        modelBuilder.Entity<ProductVariant>().Property(v => v.CostPrice).HasPrecision(12, 2);
        modelBuilder.Entity<ProductVariant>().Property(v => v.SellPrice).HasPrecision(12, 2);


        modelBuilder.Entity<Product>()
            .HasOne(p => p.Brand)
            .WithMany(b => b.Products)
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductVariant>()
            .HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductImage>()
            .HasOne(i => i.Product)
            .WithMany(p => p.Images)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductImage>()
            .HasOne(i => i.ProductVariant)
            .WithMany()
            .HasForeignKey(i => i.ProductVariantId)
            .OnDelete(DeleteBehavior.SetNull);

        // TODO(Karen): "product must have >= 1 active variant with a price" is
        // enforced in ProductService, not here. A zero-variant product is a
        // valid draft at the data layer.

        modelBuilder.Entity<Combo>().HasIndex(c => c.Slug).IsUnique();
        modelBuilder.Entity<Combo>().Property(c => c.ManualPrice).HasPrecision(12, 2);

        modelBuilder.Entity<ComboItem>()
            .HasOne(ci => ci.Combo)
            .WithMany(c => c.Items)
            .HasForeignKey(ci => ci.ComboId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade — a variant referenced by a combo can still be
        // deactivated (variants are never hard-deleted, see ADR-0007's
        // pattern), so this FK never actually blocks anything in practice.
        modelBuilder.Entity<ComboItem>()
            .HasOne(ci => ci.ProductVariant)
            .WithMany()
            .HasForeignKey(ci => ci.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
