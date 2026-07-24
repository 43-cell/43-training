using Microsoft.EntityFrameworkCore;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;
using OrderHub.Core.Services;
using OrderHub.Infrastructure.Data;

namespace OrderHub.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly OrderHubDbContext _db;

    public ProductRepository(OrderHubDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync() =>
        await _db.Products.OrderBy(p => p.Sku).ToListAsync();

    public async Task<IReadOnlyList<Product>> GetActiveAsync() =>
        await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Sku).ToListAsync();

    public Task<Product?> GetByIdAsync(int id) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IReadOnlyList<LowStockProduct>> GetLowStockAsync(int threshold, DateTime recentSalesSince)
    {
        var products = await _db.Products
            .Where(p => p.IsActive && p.StockQuantity < threshold)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync();

        var recentSales = await _db.OrderItems
            .Join(_db.Orders, oi => oi.OrderId, o => o.Id, (oi, o) => new { oi.ProductId, oi.Quantity, o.CreatedAt, o.Status })
            .Where(x => x.CreatedAt >= recentSalesSince && x.Status != OrderStatus.Cancelled)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Sold = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Sold);

        return products
            .Select(p => new LowStockProduct(p.Sku, p.Name, p.StockQuantity, recentSales.TryGetValue(p.Id, out var sold) ? sold : 0))
            .ToList();
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
