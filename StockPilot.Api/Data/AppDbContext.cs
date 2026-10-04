using Microsoft.EntityFrameworkCore;
using StockPilot.Api.Models;

namespace StockPilot.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<ProductFamily> ProductFamilies => Set<ProductFamily>();

    public DbSet<Store> Stores => Set<Store>();

    public DbSet<ProductStock> ProductStocks => Set<ProductStock>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Product>()
            .HasOne(p => p.Supplier)
            .WithMany(s => s.Products)
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Product>()
            .HasOne(p => p.ProductFamily)
            .WithMany(f => f.Products)
            .HasForeignKey(p => p.ProductFamilyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(u => u.Store)
            .WithMany()
            .HasForeignKey(u => u.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Store>()
            .HasIndex(s => s.IsCentral)
            .IsUnique()
            .HasFilter("\"IsCentral\" = true");

        modelBuilder.Entity<User>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_Users_StoreId_Required_Unless_Admin",
                "\"Role\" = 'Admin' OR \"StoreId\" IS NOT NULL"));

        modelBuilder.Entity<ProductStock>()
            .HasOne(ps => ps.Product)
            .WithMany(p => p.ProductStocks)
            .HasForeignKey(ps => ps.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductStock>()
            .HasOne(ps => ps.Store)
            .WithMany()
            .HasForeignKey(ps => ps.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductStock>()
            .HasIndex(ps => new { ps.ProductId, ps.StoreId })
            .IsUnique();
    }


}
