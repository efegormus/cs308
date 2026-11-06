using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Dtos;

namespace OnlineStore.Tests;

// PUT /api/cart/items/{productId} (the quantity buttons of the cart page). Shares the helpers of CartEndpointsTests.
public partial class CartEndpointsTests
{
    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", await CsrfTokenAsync(client));
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task SetQuantity_RaisesAndLowersTheLine()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });

        var raised = await PutAsync(client, "/api/cart/items/10", new { quantity = 4 });
        var lowered = await PutAsync(client, "/api/cart/items/10", new { quantity = 2 });

        Assert.Equal(HttpStatusCode.OK, raised.StatusCode);
        Assert.Equal(4, Assert.Single((await raised.Content.ReadFromJsonAsync<CartDto>())!.Items).Quantity);
        var cart = (await lowered.Content.ReadFromJsonAsync<CartDto>())!;
        var line = Assert.Single(cart.Items);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(line.UnitPrice * 2, line.LineTotal);
        Assert.Equal(cart.Total, line.LineTotal);
    }

    [Fact]
    public async Task SetQuantity_ForACustomer_ChangesTheAccountCart()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await LoginNewCustomerAsync(client);
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });

        var response = await PutAsync(client, "/api/cart/items/10", new { quantity = 3 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, (await client.GetFromJsonAsync<CartDto>("/api/cart"))!.ItemCount);
    }

    [Fact]
    public async Task SetQuantity_MoreThanTheStock_Returns409AndLeavesTheLine()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());   // Product 11 has a stock of 1.
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 11, quantity = 1 });

        var response = await PutAsync(client, "/api/cart/items/11", new { quantity = 2 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, Assert.Single((await client.GetFromJsonAsync<CartDto>("/api/cart"))!.Items).Quantity);
    }

    [Fact]
    public async Task SetQuantity_ProductThatIsNotInTheCart_Returns404()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });

        var response = await PutAsync(client, "/api/cart/items/11", new { quantity = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetQuantity_WithoutACart_Returns404AndCreatesNone()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();

        var response = await PutAsync(client, "/api/cart/items/10", new { quantity = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await factory.QueryAsync(db => db.Carts.ToListAsync()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public async Task SetQuantity_OutsideTheAllowedRange_Returns400(int quantity)
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });

        var response = await PutAsync(client, "/api/cart/items/10", new { quantity });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, Assert.Single((await client.GetFromJsonAsync<CartDto>("/api/cart"))!.Items).Quantity);
    }

    [Fact]
    public async Task SetQuantity_ForADeactivatedProduct_Returns409()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });
        await factory.UpdateAsync(async db =>
        {
            (await db.Products.SingleAsync(p => p.Id == 10)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await PutAsync(client, "/api/cart/items/10", new { quantity = 2 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SetQuantity_WithAnUnknownField_Returns400()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });

        var response = await PutAsync(client, "/api/cart/items/10", new { quantity = 2, price = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetQuantity_WithoutCsrfToken_Returns400()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });

        var response = await client.PutAsJsonAsync("/api/cart/items/10", new { quantity = 2 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
