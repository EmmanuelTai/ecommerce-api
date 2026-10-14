using ECommerce.Core.Entities;
using ECommerce.Core.Enums;
using ECommerce.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Data.Seeding;

public class DbSeeder(AppDbContext db, IOptions<SeedSettings> options, ILogger<DbSeeder> logger)
{
    // Each +1 doubles the hashing time. 12 takes roughly 250ms: slow enough to hurt brute force,
    // fast enough for one login. The library default is 11.
    private const int BCryptWorkFactor = 12;

    // includeTestCustomer is only true in Development: a known customer login must never exist in production.
    public async Task SeedAsync(bool includeTestCustomer, CancellationToken cancellationToken = default)
    {
        // Only seed an empty database, so restarting the app never duplicates or overwrites data.
        if (await db.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Database already has data; skipping seed");
            return;
        }

        var settings = options.Value;

        // The admin can't register through the API (register always creates a Customer),
        // so the only way an admin account exists is through this seed.
        db.Users.Add(new User
        {
            FullName = "Store Admin",
            Email = settings.AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(settings.AdminPassword, BCryptWorkFactor),
            Role = Role.Admin
        });

        if (includeTestCustomer)
        {
            if (string.IsNullOrWhiteSpace(settings.CustomerEmail) || string.IsNullOrWhiteSpace(settings.CustomerPassword))
            {
                throw new InvalidOperationException(
                    "Seed:CustomerEmail and Seed:CustomerPassword must be set to seed the test customer.");
            }

            // Customers get an empty cart, the same as registering through the API.
            db.Users.Add(new User
            {
                FullName = "Test Customer",
                Email = settings.CustomerEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(settings.CustomerPassword, BCryptWorkFactor),
                Phone = "08012345678",
                Role = Role.Customer,
                Cart = new Cart()
            });
        }

        db.Categories.AddRange(CreateCategories());

        // One SaveChanges call runs in a single transaction: all of the seed is saved, or none of it.
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded database: {Users} users, {Categories} categories, {Products} products",
            await db.Users.CountAsync(cancellationToken),
            await db.Categories.CountAsync(cancellationToken),
            await db.Products.CountAsync(cancellationToken));
    }

    private static List<Category> CreateCategories() =>
    [
        new()
        {
            Name = "Electronics",
            Slug = "electronics",
            Products =
            [
                Product("Wireless Earbuds", "wireless-earbuds", "Bluetooth 5.3 earbuds with charging case and 24-hour battery.", 25_000m, 40),
                Product("Smartphone 128GB", "smartphone-128gb", "6.5-inch display, dual camera, 128GB storage.", 185_000m, 15),
                Product("Laptop 14-inch", "laptop-14-inch", "Core i5, 16GB RAM, 512GB SSD.", 650_000m, 8),
                Product("Power Bank 20000mAh", "power-bank-20000mah", "Fast-charging power bank with two USB ports.", 18_500m, 60),
                Product("Smart Watch", "smart-watch", "Heart-rate, step tracking and notifications.", 45_000m, 3)
            ]
        },
        new()
        {
            Name = "Fashion",
            Slug = "fashion",
            Products =
            [
                Product("Ankara Shirt", "ankara-shirt", "Short-sleeve cotton shirt in Ankara print.", 12_000m, 30),
                Product("Leather Sandals", "leather-sandals", "Handmade leather sandals.", 15_500m, 25),
                Product("Denim Jeans", "denim-jeans", "Slim-fit stretch denim.", 18_000m, 35),
                Product("Canvas Sneakers", "canvas-sneakers", "Lightweight everyday sneakers.", 22_000m, 20),
                Product("Leather Belt", "leather-belt", "Genuine leather belt with metal buckle.", 7_500m, 2)
            ]
        },
        new()
        {
            Name = "Home & Kitchen",
            Slug = "home-and-kitchen",
            Products =
            [
                Product("Electric Kettle 1.7L", "electric-kettle-1-7l", "Stainless steel kettle with auto shut-off.", 16_000m, 30),
                Product("Blender 1.5L", "blender-1-5l", "500W blender with glass jar.", 38_000m, 12),
                Product("Non-stick Pot Set", "non-stick-pot-set", "Set of three non-stick pots with lids.", 42_000m, 10),
                Product("Rechargeable Fan", "rechargeable-fan", "16-inch fan with built-in battery and LED light.", 55_000m, 18),
                Product("Bedsheet Set", "bedsheet-set", "Double bedsheet with two pillowcases.", 14_000m, 22)
            ]
        },
        new()
        {
            Name = "Books",
            Slug = "books",
            Products =
            [
                Product("Things Fall Apart", "things-fall-apart", "Chinua Achebe's classic novel.", 6_500m, 50),
                Product("Purple Hibiscus", "purple-hibiscus", "Novel by Chimamanda Ngozi Adichie.", 7_000m, 40),
                Product("Clean Code", "clean-code", "A handbook of agile software craftsmanship.", 28_000m, 10),
                Product("The Pragmatic Programmer", "the-pragmatic-programmer", "From journeyman to master.", 30_000m, 6),
                Product("C# in Depth", "csharp-in-depth", "Deep dive into the C# language.", 32_000m, 0)
            ]
        }
    ];

    private static Product Product(string name, string slug, string description, decimal price, int stock) => new()
    {
        Name = name,
        Slug = slug,
        Description = description,
        Price = price,
        StockQuantity = stock
    };
}
