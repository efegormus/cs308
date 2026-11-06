using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Accounts;
using OnlineStore.Dtos;
using OnlineStore.Models;
using OnlineStore.Shopping;

namespace OnlineStore.Tests;

// POST /api/cart/merge (SCRUM-100). Shares the helpers and the factory of CartEndpointsTests.
public partial class CartEndpointsTests
{
    private static async Task LogoutAsync(HttpClient client) =>
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post, "/api/auth/logout")).StatusCode);

    private static async Task LoginAgainAsync(HttpClient client) =>
        Assert.Equal(HttpStatusCode.OK,
            (await PostAsync(client, "/api/auth/login", new LoginRequest("cart@example.com", Password))).StatusCode);

    private static async Task<CartMergeDto> MergeAsync(HttpClient client)
    {
        var response = await SendAsync(client, HttpMethod.Post, "/api/cart/merge");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CartMergeDto>())!;
    }

    // A customer with one account cart who is now a guest with a second cart, then logs in again.
    private static async Task CustomerWithAccountCartAndGuestCartAsync(
        HttpClient client, (int ProductId, int Quantity) account, (int ProductId, int Quantity) guest)
    {
        await LoginNewCustomerAsync(client);
        await PostAsync(client, "/api/cart/items", new { productId = account.ProductId, quantity = account.Quantity });
        await LogoutAsync(client);
        await PostAsync(client, "/api/cart/items", new { productId = guest.ProductId, quantity = guest.Quantity });
        await LoginAgainAsync(client);
    }

    [Fact]
    public async Task Merge_CustomerWithoutACart_TakesOverTheGuestCart()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 2 });
        var userId = await LoginNewCustomerAsync(client);

        var response = await SendAsync(client, HttpMethod.Post, "/api/cart/merge");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<CartMergeDto>())!;
        Assert.True(result.Merged);
        Assert.Empty(result.Conflicts);
        Assert.Equal(2, Assert.Single(result.Cart.Items).Quantity);
        var stored = Assert.Single(await factory.QueryAsync(db => db.Carts.ToListAsync()));
        Assert.Equal(userId, stored.UserId);
        Assert.Null(stored.GuestTokenHash);
        // The cookie is deleted, and the account cart is what the customer sees now.
        Assert.Contains(response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(CartEndpoints.GuestCookie + "=") && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, (await client.GetFromJsonAsync<CartDto>("/api/cart"))!.ItemCount);
    }

    [Fact]
    public async Task Merge_BothCarts_AddsQuantitiesAndMovesNewLines()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await LoginNewCustomerAsync(client);
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });
        await LogoutAsync(client);
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 2 });
        await PostAsync(client, "/api/cart/items", new { productId = 11, quantity = 1 });
        await LoginAgainAsync(client);

        var result = await MergeAsync(client);

        Assert.True(result.Merged);
        Assert.Empty(result.Conflicts);
        Assert.Equal(3, result.Cart.Items.Single(line => line.ProductId == 10).Quantity);
        Assert.Equal(1, result.Cart.Items.Single(line => line.ProductId == 11).Quantity);
        var carts = await factory.QueryAsync(db => db.Carts.ToListAsync());
        Assert.Single(carts, cart => cart.State == CartState.Open);
        Assert.Single(carts, cart => cart.State == CartState.Abandoned);
        Assert.Equal(2, (await factory.QueryAsync(db => db.CartItems.ToListAsync())).Count);
    }

    [Fact]
    public async Task Merge_IsSafeToRepeat()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await CustomerWithAccountCartAndGuestCartAsync(client, (10, 1), (10, 2));

        await MergeAsync(client);
        var second = await MergeAsync(client);

        Assert.False(second.Merged);
        Assert.Equal(3, Assert.Single(second.Cart.Items).Quantity);
    }

    [Fact]
    public async Task Merge_MoreThanTheStock_KeepsTheQuantityAndReportsTheConflict()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());   // Product 11 has a stock of 1.
        using var client = factory.CreateClient();
        await CustomerWithAccountCartAndGuestCartAsync(client, (11, 1), (11, 1));

        var result = await MergeAsync(client);

        var line = Assert.Single(result.Cart.Items);
        Assert.Equal(2, line.Quantity);
        Assert.False(line.Available);
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal((11, "NotEnoughStock", 2, 1), (conflict.ProductId, conflict.Reason, conflict.Quantity, conflict.StockQuantity));
    }

    [Fact]
    public async Task Merge_ProductDeactivatedSinceItWasAdded_IsKeptAndReported()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });
        await LoginNewCustomerAsync(client);
        await factory.UpdateAsync(async db =>
        {
            (await db.Products.SingleAsync(p => p.Id == 10)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var result = await MergeAsync(client);

        Assert.Single(result.Cart.Items);
        Assert.Equal("Unavailable", Assert.Single(result.Conflicts).Reason);
    }

    [Fact]
    public async Task Merge_AccountCartWithMaxLines_LeavesTheExtraLineInTheGuestCart()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog().Append(new Category
        {
            Id = 2, Name = "Many",
            Products = Enumerable.Range(100, CartEndpoints.MaxLines)
                .Select(id => new Product { Id = id, Sku = $"M-{id}", Name = $"Many {id}", Price = 1m, StockQuantity = 5 })
                .ToList()
        }).ToArray());
        using var client = factory.CreateClient();
        var userId = await LoginNewCustomerAsync(client);
        await factory.UpdateAsync(async db =>
        {
            var account = new Cart { UserId = userId };
            account.Items = Enumerable.Range(100, CartEndpoints.MaxLines)
                .Select(id => new CartItem { ProductId = id, Quantity = 1 }).ToList();
            db.Carts.Add(account);
            await db.SaveChangesAsync();
        });
        await LogoutAsync(client);
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });
        await LoginAgainAsync(client);

        var response = await SendAsync(client, HttpMethod.Post, "/api/cart/merge");

        var result = (await response.Content.ReadFromJsonAsync<CartMergeDto>())!;
        Assert.Equal(CartEndpoints.MaxLines, result.Cart.Items.Count);
        Assert.Equal("CartFull", Assert.Single(result.Conflicts).Reason);
        var guest = Assert.Single(await factory.QueryAsync(db => db.Carts.Where(c => c.UserId == null).ToListAsync()));
        Assert.Equal(CartState.Open, guest.State);
        Assert.Single(await factory.QueryAsync(db => db.CartItems.Where(i => i.CartId == guest.Id).ToListAsync()));
        // The cookie stays, so a later merge can try again.
        Assert.DoesNotContain(response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : Array.Empty<string>(),
            value => value.StartsWith(CartEndpoints.GuestCookie + "="));
    }

    [Fact]
    public async Task Merge_WithoutAGuestCart_ReturnsTheAccountCart()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await LoginNewCustomerAsync(client);
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 2 });

        var result = await MergeAsync(client);

        Assert.False(result.Merged);
        Assert.Equal(2, result.Cart.ItemCount);
        Assert.Single(await factory.QueryAsync(db => db.Carts.ToListAsync()));
    }

    [Fact]
    public async Task Merge_CustomerWithTwoOpenCarts_GetsAMergeRequiredConflict()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });
        var userId = await LoginNewCustomerAsync(client);
        await factory.UpdateAsync(async db =>
        {
            db.Carts.AddRange(new Cart { UserId = userId }, new Cart { UserId = userId });
            await db.SaveChangesAsync();
        });

        var response = await SendAsync(client, HttpMethod.Post, "/api/cart/merge");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        // Nothing was lost: the guest cart is still the guest's.
        Assert.Single(await factory.QueryAsync(db => db.Carts.Where(c => c.UserId == null).ToListAsync()));
    }

    [Fact]
    public async Task Merge_CannotTakeOverAnotherVisitorsGuestCart()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var visitor = factory.CreateClient();      // The guest cart and its cookie belong to this client.
        using var customer = factory.CreateClient();     // This client never had that cookie.
        await PostAsync(visitor, "/api/cart/items", new { productId = 10, quantity = 2 });
        await LoginNewCustomerAsync(customer);

        var result = await MergeAsync(customer);

        Assert.False(result.Merged);
        Assert.Empty(result.Cart.Items);
        var guestCart = Assert.Single(await factory.QueryAsync(db => db.Carts.Where(c => c.UserId == null).ToListAsync()));
        Assert.Equal(CartState.Open, guestCart.State);
        Assert.Equal(2, (await visitor.GetFromJsonAsync<CartDto>("/api/cart"))!.ItemCount);
    }

    [Fact]
    public async Task Merge_Guest_Returns401()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();

        var response = await SendAsync(client, HttpMethod.Post, "/api/cart/merge");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Merge_WithoutCsrfToken_Returns400()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await LoginNewCustomerAsync(client);

        var response = await client.PostAsync("/api/cart/merge", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
