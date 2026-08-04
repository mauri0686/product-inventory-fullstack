using Microsoft.EntityFrameworkCore;
using ProductInventory.Domain.Entities;
using ProductInventory.Infrastructure.Data.Configurations;

namespace ProductInventory.Infrastructure.Data;

public class ProductDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();

    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
