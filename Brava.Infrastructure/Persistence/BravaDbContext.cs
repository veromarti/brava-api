using Brava.Application;
using Brava.Domain.Admins;
using Brava.Domain.Brands;
using Brava.Domain.Categories;
using Brava.Domain.Combos;
using Brava.Domain.Customers;
using Brava.Domain.Delivery;
using Brava.Domain.Orders;
using Brava.Domain.Products;
using Brava.Infrastructure.Persistence.Seeding;
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
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

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

        // --- Orders (Phase 1) --------------------------------------------------

        modelBuilder.Entity<DeliveryZone>().HasIndex(z => z.Name).IsUnique();
        modelBuilder.Entity<DeliveryZone>().Property(z => z.Price).HasPrecision(12, 2);
        modelBuilder.Entity<DeliveryZone>().HasData(DeliveryZoneSeedData.Zones);

        modelBuilder.Entity<Customer>().HasIndex(c => c.Phone).IsUnique();

        modelBuilder.Entity<Order>().HasIndex(o => o.Number).IsUnique();
        modelBuilder.Entity<Order>().HasIndex(o => o.Sequence).IsUnique();
        modelBuilder.Entity<Order>().Property(o => o.Subtotal).HasPrecision(12, 2);
        modelBuilder.Entity<Order>().Property(o => o.DeliveryFee).HasPrecision(12, 2);
        modelBuilder.Entity<Order>().Property(o => o.Total).HasPrecision(12, 2);

        // SetNull everywhere the link is optional and the order already
        // snapshots what it needs to display/cost — deleting a customer, zone,
        // variant or combo must never delete or block a historical order.
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.DeliveryZone)
            .WithMany()
            .HasForeignKey(o => o.DeliveryZoneId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<OrderItem>().Property(i => i.UnitPrice).HasPrecision(12, 2);
        modelBuilder.Entity<OrderItem>().Property(i => i.UnitCost).HasPrecision(12, 2);
        modelBuilder.Entity<OrderItem>().Property(i => i.LineTotal).HasPrecision(12, 2);

        modelBuilder.Entity<OrderItem>()
            .HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderItem>()
            .HasOne(i => i.ProductVariant)
            .WithMany()
            .HasForeignKey(i => i.ProductVariantId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<OrderItem>()
            .HasOne(i => i.Combo)
            .WithMany()
            .HasForeignKey(i => i.ComboId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
