using ECommerce.Core.DTOs.Admin;
using ECommerce.Core.DTOs.Catalog;
using ECommerce.Core.DTOs.Common;
using ECommerce.Core.Entities;
using ECommerce.Core.Exceptions;
using ECommerce.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerce.Core.Services;

// Cache rules. Two kinds of entry exist: the category list (categories:all) and single products (products:{id}),
// and a cached ProductDto includes its CategoryName.
//   - Product updated or deactivated → remove products:{id}.
//   - Product created                → nothing: only products that were found are cached, so a new ID has no entry.
//   - Category created or deleted    → remove categories:all (a deleted category has no products, so no product entries).
//   - Category renamed               → remove categories:all AND products:{id} for every product in it,
//                                      because each of those cached products still shows the old category name.
// The category list holds no product data, so product changes never need to clear it.
// Entries are removed after SaveChanges: removing them earlier would let another request re-cache the old values
// from the database before the change is saved.
public class AdminCatalogService(IUnitOfWork unitOfWork, IMemoryCache cache) : IAdminCatalogService
{
    public Task<PagedResult<AdminProductDto>> GetProductsAsync(AdminProductQuery query, CancellationToken cancellationToken = default) =>
        unitOfWork.Products.GetAdminDtosAsync(query, cancellationToken);

    // Inactive products are found too: admins need to see them to reactivate them.
    public async Task<AdminProductDto> GetProductAsync(int id, CancellationToken cancellationToken = default) =>
        await unitOfWork.Products.GetAdminDtoByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

    public async Task<AdminProductDto> CreateProductAsync(SaveProductRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        var name = request.Name.Trim();
        var slug = await CreateProductSlugAsync(name, null, cancellationToken);

        var product = new Product
        {
            CategoryId = request.CategoryId,
            Name = name,
            Slug = slug,
            Description = request.Description.Trim(),
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            ImageUrl = request.ImageUrl,
            IsActive = request.IsActive
        };

        unitOfWork.Products.Add(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetProductAsync(product.Id, cancellationToken);
    }

    public async Task<AdminProductDto> UpdateProductAsync(int id, SaveProductRequest request, CancellationToken cancellationToken = default)
    {
        // Tracked: SaveChanges compares each property with the value it loaded and only writes the ones that changed.
        var product = await unitOfWork.Products.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        var name = request.Name.Trim();

        product.CategoryId = request.CategoryId;
        product.Name = name;
        product.Slug = await CreateProductSlugAsync(name, id, cancellationToken);
        product.Description = request.Description.Trim();
        product.Price = request.Price;
        product.ImageUrl = request.ImageUrl;
        product.IsActive = request.IsActive;

        // The admin sets the counted stock level; it isn't a "+/- quantity" change like checkout's, so there is
        // no WHERE stock >= qty condition. Because EF only writes changed columns, a PUT that leaves the number
        // as it was doesn't touch StockQuantity, and can't undo a checkout that ran in the meantime.
        product.StockQuantity = request.StockQuantity;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Price, stock, name, category or IsActive may all have changed; the cached copy is wrong in any case.
        cache.Remove(CacheKeys.Product(id));

        return await GetProductAsync(id, cancellationToken);
    }

    // Soft delete: order items still point at the product, and an old order must keep its link.
    // Carts that hold it show it as unavailable, and checkout refuses it (its stock UPDATE requires IsActive).
    public async Task DeleteProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await unitOfWork.Products.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        product.IsActive = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Otherwise GET /api/products/{id} would keep showing it for up to 10 minutes.
        cache.Remove(CacheKeys.Product(id));
    }

    public async Task<CategoryDto> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var category = new Category
        {
            Name = name,
            Slug = await CreateCategorySlugAsync(name, null, cancellationToken)
        };

        unitOfWork.Categories.Add(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKeys.Categories);

        return new CategoryDto(category.Id, category.Name, category.Slug);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(int id, SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await unitOfWork.Categories.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Category not found.");

        var name = request.Name.Trim();
        category.Name = name;
        category.Slug = await CreateCategorySlugAsync(name, id, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKeys.Categories);

        // Each cached product in this category carries the old CategoryName. The cache can't be searched by
        // category, so ask the database which products are in it and remove each one's entry. Keys that were
        // never cached are simply ignored by Remove.
        var productIds = await unitOfWork.Products.GetIdsByCategoryIdAsync(id, cancellationToken);
        foreach (var productId in productIds)
        {
            cache.Remove(CacheKeys.Product(productId));
        }

        return new CategoryDto(category.Id, category.Name, category.Slug);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await unitOfWork.Categories.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Category not found.");

        // Products are soft-deleted, so even a category of only inactive products still has rows pointing at it.
        // The foreign key (Restrict) would refuse the delete anyway; checking first gives a clear 409 instead of a 500.
        if (await unitOfWork.Categories.HasProductsAsync(id, cancellationToken))
        {
            throw new ConflictException("This category still has products. Move or remove them first.");
        }

        unitOfWork.Categories.Remove(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKeys.Categories);
    }

    // 400 rather than 404: the URL was fine, a value in the body was wrong.
    private async Task EnsureCategoryExistsAsync(int categoryId, CancellationToken cancellationToken)
    {
        if (!await unitOfWork.Categories.ExistsAsync(categoryId, cancellationToken))
        {
            throw new BadRequestException("Category does not exist.");
        }
    }

    // Friendly 409 for the normal case; the unique index on Slug still catches two requests racing
    // with the same name (UnitOfWork turns that into a 409 too).
    private async Task<string> CreateProductSlugAsync(string name, int? productId, CancellationToken cancellationToken)
    {
        var slug = CreateSlug(name);
        if (await unitOfWork.Products.SlugExistsAsync(slug, productId, cancellationToken))
        {
            throw new ConflictException("A product with this name already exists.");
        }

        return slug;
    }

    // Two names that make the same slug ("Books" and "books!") count as duplicates, which also covers the
    // unique index on Name.
    private async Task<string> CreateCategorySlugAsync(string name, int? categoryId, CancellationToken cancellationToken)
    {
        var slug = CreateSlug(name);
        if (await unitOfWork.Categories.SlugExistsAsync(slug, categoryId, cancellationToken))
        {
            throw new ConflictException("A category with this name already exists.");
        }

        return slug;
    }

    private static string CreateSlug(string name)
    {
        var slug = Slug.From(name);
        return slug.Length > 0
            ? slug
            : throw new BadRequestException("Name must contain at least one letter or digit.");
    }
}
