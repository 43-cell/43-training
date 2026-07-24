using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class ProductServiceTests
{
    [Fact]
    public async Task GetAll_ReturnsAllProductsIncludingInactive()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetAllAsync();

        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task GetActive_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetActiveAsync();

        Assert.All(products, p => Assert.True(p.IsActive));
        Assert.Single(products);
    }

    [Fact]
    public async Task GetLowStock_FiltersByThresholdAndSortsByStockAscending()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-B001", stock: 15);
        TestSetup.AddProduct(db, sku: "SKU-B002", stock: 3);
        TestSetup.AddProduct(db, sku: "SKU-B003", stock: 8);

        var result = await service.GetLowStockAsync(10);

        Assert.Equal(2, result.Count);
        Assert.Equal("SKU-B002", result[0].Sku);
        Assert.Equal("SKU-B003", result[1].Sku);
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-C001", stock: 5, isActive: false);

        var result = await service.GetLowStockAsync(10);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLowStock_RecentSoldQuantity_ExcludesCancelledOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, sku: "SKU-D001", stock: 5);

        db.Orders.AddRange(
            new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 3, UnitPriceSnapshot = product.UnitPrice } }
            },
            new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Cancelled,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 5, UnitPriceSnapshot = product.UnitPrice } }
            });
        db.SaveChanges();

        var result = await service.GetLowStockAsync(10);

        Assert.Equal(3, result.Single(p => p.Sku == "SKU-D001").RecentSoldQuantity);
    }
}
