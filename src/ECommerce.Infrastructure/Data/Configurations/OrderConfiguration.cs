using ECommerce.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.OrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(o => o.ShippingFullName).HasMaxLength(100).IsRequired();
        builder.Property(o => o.ShippingPhone).HasMaxLength(20).IsRequired();
        builder.Property(o => o.ShippingStreet).HasMaxLength(200).IsRequired();
        builder.Property(o => o.ShippingCity).HasMaxLength(100).IsRequired();
        builder.Property(o => o.ShippingState).HasMaxLength(100).IsRequired();

        builder.HasIndex(o => o.OrderNumber).IsUnique();

        // Restrict: order history must survive, so a user with orders can't be hard-deleted.
        builder.HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
