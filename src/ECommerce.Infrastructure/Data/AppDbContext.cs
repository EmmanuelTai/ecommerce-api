using ECommerce.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ECommerce.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration<T> in Data/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Rules for every property of a type, set once instead of per entity.
        // Money is decimal(18,2): exact base-10 values, no floating-point rounding errors.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        // Enums stored as readable strings ("Pending"), so reordering an enum can't corrupt data.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(20);

        // SQL Server's datetime2 has no time zone, so EF reads values back as DateTimeKind.Unspecified and
        // JSON shows them without a "Z". We only ever store UTC, so mark every DateTime read as UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    // Writes the value unchanged; on read, labels it as UTC (SpecifyKind doesn't shift the time).
    private class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SetTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Fills CreatedAt/UpdatedAt for any entity that has them, so services never set them by hand.
    // Note: ExecuteUpdateAsync bypasses SaveChanges, so bulk updates must set UpdatedAt themselves.
    private void SetTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            var hasCreatedAt = entry.Metadata.FindProperty("CreatedAt") is not null;
            var hasUpdatedAt = entry.Metadata.FindProperty("UpdatedAt") is not null;

            if (entry.State == EntityState.Added)
            {
                if (hasCreatedAt) entry.Property("CreatedAt").CurrentValue = now;
                if (hasUpdatedAt) entry.Property("UpdatedAt").CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                // CreatedAt must never change after insert.
                if (hasCreatedAt) entry.Property("CreatedAt").IsModified = false;
                if (hasUpdatedAt) entry.Property("UpdatedAt").CurrentValue = now;
            }
        }
    }
}
