using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OnlineStore.Accounts;
using OnlineStore.Data;
using OnlineStore.Dtos;
using OnlineStore.Models;

namespace OnlineStore.Shopping;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddCartItemRequest(int ProductId, int Quantity);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetCartItemQuantityRequest(int Quantity);

// Shopping cart API (SCRUM-49). Guests and customers use the same routes:
// a customer's cart belongs to the account, a guest's cart is found through a random token
// kept in an HttpOnly cookie (only its SHA-256 hash is stored in the database).
public static class CartEndpoints
{
    public const string GuestCookie = "OnlineStore.Cart";
    public const int MaxQuantityPerRequest = 99;
    // Checkout accepts carts with 1 to 100 product lines (docs/purchasing.md).
    public const int MaxLines = 100;
    private static readonly TimeSpan GuestCartLifetime = TimeSpan.FromDays(30);

    public static void MapCartEndpoints(this WebApplication app)
    {
        var cart = app.MapGroup("/api/cart");
        cart.AddEndpointFilter(PrivateRequest);

        // GET /api/cart -> the current cart (empty when there is none yet).
        cart.MapGet("", async (HttpContext context, StoreDbContext db, CancellationToken cancellationToken) =>
        {
            if (!MayUseCart(context)) return NotForStaff();
            var (found, mergeRequired) = await FindCartAsync(context, db, cancellationToken);
            if (mergeRequired) return MergeRequired();
            return Results.Ok(found is null ? CartDto.Empty : await LoadAsync(db, found.Id, cancellationToken));
        }).AllowAnonymous();

        // POST /api/cart/items { productId, quantity } -> adds units to the cart.
        cart.MapPost("/items", async (
            AddCartItemRequest input, HttpContext context, StoreDbContext db, CancellationToken cancellationToken) =>
        {
            if (!MayUseCart(context)) return NotForStaff();

            var errors = new Dictionary<string, string[]>();
            if (input.ProductId < 1) errors["productId"] = ["productId must be a positive integer."];
            if (input.Quantity < 1 || input.Quantity > MaxQuantityPerRequest)
                errors["quantity"] = [$"quantity must be between 1 and {MaxQuantityPerRequest}."];
            if (errors.Count != 0) return Results.ValidationProblem(errors);

            var product = await db.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == input.ProductId && p.IsActive, cancellationToken);
            if (product is null)
                return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                    title: "Product not found.", detail: "No active product has this id.");

            // The server enforces stock; hiding the button in the page is not enough.
            if (product.StockQuantity < 1)
                return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "This product is out of stock.");

            // Lock order matches checkout: customer row first, then the cart row.
            await using var transaction = await BeginAsync(context, db, cancellationToken);
            var (found, mergeRequired) = await FindCartAsync(context, db, cancellationToken);
            if (mergeRequired) return MergeRequired();
            var current = found is null ? null : await LockCartAsync(db, found.Id, cancellationToken);
            if (found is not null && current is null) return CartChanged();

            var line = current is null ? null
                : await db.CartItems.FirstOrDefaultAsync(
                    item => item.CartId == current.Id && item.ProductId == product.Id, cancellationToken);

            var wanted = (line?.Quantity ?? 0) + input.Quantity;
            if (wanted > product.StockQuantity)
                return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "Not enough stock.",
                    detail: $"Only {product.StockQuantity} unit(s) are available.");

            if (line is null && current is not null
                && await db.CartItems.CountAsync(item => item.CartId == current.Id, cancellationToken) >= MaxLines)
                return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "The cart is full.", detail: $"A cart can hold at most {MaxLines} different products.");

            if (current is null)
            {
                current = CreateCart(context);
                db.Carts.Add(current);
            }

            if (line is null) current.Items.Add(new CartItem { ProductId = product.Id, Quantity = input.Quantity });
            else line.Quantity = wanted;
            current.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);

            return Results.Ok(await LoadAsync(db, current.Id, cancellationToken));
        }).AllowAnonymous().AddEndpointFilter<CsrfFilter>();

        // DELETE /api/cart/items/{productId} -> removes the whole line. Removing a product that is
        // not in the cart is not an error, so a double click does nothing harmful.
        // PUT /api/cart/items/{productId} -> sets the quantity of a line that is already in the cart (the cart
        // page's minus and plus buttons). The stock is checked again here; a line is emptied with DELETE.
        cart.MapPut("/items/{productId:int}", async (
            int productId, SetCartItemQuantityRequest input, HttpContext context, StoreDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!MayUseCart(context)) return NotForStaff();
            if (input.Quantity < 1 || input.Quantity > MaxQuantityPerRequest)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["quantity"] = [$"quantity must be between 1 and {MaxQuantityPerRequest}. Remove the line to empty it."]
                });

            // Lock order matches checkout: customer row first, then the cart row.
            await using var transaction = await BeginAsync(context, db, cancellationToken);
            var (found, mergeRequired) = await FindCartAsync(context, db, cancellationToken);
            if (mergeRequired) return MergeRequired();
            var current = found is null ? null : await LockCartAsync(db, found.Id, cancellationToken);
            if (found is not null && current is null) return CartChanged();

            var line = current is null ? null
                : await db.CartItems.Include(item => item.Product).FirstOrDefaultAsync(
                    item => item.CartId == current.Id && item.ProductId == productId, cancellationToken);
            if (line is null)
                return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                    title: "This product is not in the cart.");

            if (!line.Product.IsActive)
                return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "This product is no longer available.", detail: "Remove it from the cart.");
            if (input.Quantity > line.Product.StockQuantity)
                return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "Not enough stock.",
                    detail: $"Only {line.Product.StockQuantity} unit(s) are available.");

            line.Quantity = input.Quantity;
            current!.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);

            return Results.Ok(await LoadAsync(db, current.Id, cancellationToken));
        }).AllowAnonymous().AddEndpointFilter<CsrfFilter>();

        cart.MapDelete("/items/{productId:int}", async (
            int productId, HttpContext context, StoreDbContext db, CancellationToken cancellationToken) =>
        {
            if (!MayUseCart(context)) return NotForStaff();
            await using var transaction = await BeginAsync(context, db, cancellationToken);
            var (found, mergeRequired) = await FindCartAsync(context, db, cancellationToken);
            if (mergeRequired) return MergeRequired();
            if (found is null) return Results.Ok(CartDto.Empty);
            var current = await LockCartAsync(db, found.Id, cancellationToken);
            if (current is null) return CartChanged();

            var line = await db.CartItems.FirstOrDefaultAsync(
                item => item.CartId == current.Id && item.ProductId == productId, cancellationToken);
            if (line is not null)
            {
                db.CartItems.Remove(line);
                current.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);

            return Results.Ok(await LoadAsync(db, current.Id, cancellationToken));
        }).AllowAnonymous().AddEndpointFilter<CsrfFilter>();

        // POST /api/cart/merge -> moves the guest cart (found through the cookie) into the customer's cart.
        // A hook runs it right after login (docs/authentication.md), and the redirect can cut it off, so
        // repeating it is safe: without a guest cart it only returns the current cart.
        cart.MapPost("/merge", async (HttpContext context, StoreDbContext db, CancellationToken cancellationToken) =>
        {
            if (!MayUseCart(context)) return NotForStaff();
            var userId = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Lock order matches checkout: customer row, then the customer's cart, then the guest cart.
            await using var transaction = await BeginAsync(context, db, cancellationToken);
            var customerCarts = await db.Carts.AsNoTracking()
                .Where(c => c.State == CartState.Open && c.UserId == userId)
                .Take(2).ToListAsync(cancellationToken);
            if (customerCarts.Count > 1) return MergeRequired();
            var customerFound = customerCarts.SingleOrDefault();

            var token = context.Request.Cookies[GuestCookie];
            Cart? guestFound = null;
            if (IsValidToken(token))
            {
                var hash = HashToken(token!);
                guestFound = await db.Carts.AsNoTracking().FirstOrDefaultAsync(
                    c => c.State == CartState.Open && c.UserId == null && c.GuestTokenHash == hash, cancellationToken);
            }

            if (guestFound is null)
            {
                // Nothing to move. A cookie that points to no open guest cart is stale, so it is dropped.
                if (token is not null) DeleteGuestCookie(context);
                return Results.Ok(new CartMergeDto(
                    customerFound is null ? CartDto.Empty : await LoadAsync(db, customerFound.Id, cancellationToken),
                    false, Array.Empty<CartMergeConflictDto>()));
            }

            var customer = customerFound is null ? null : await LockCartAsync(db, customerFound.Id, cancellationToken);
            var guest = await LockCartAsync(db, guestFound.Id, cancellationToken);
            if (guest is null || (customerFound is not null && customer is null)) return CartChanged();

            var guestItems = await db.CartItems.Include(item => item.Product)
                .Where(item => item.CartId == guest.Id).OrderBy(item => item.Id).ToListAsync(cancellationToken);
            var conflicts = new List<CartMergeConflictDto>();
            var now = DateTime.UtcNow;

            if (customer is null)
            {
                // The account has no open cart: the guest cart simply becomes the account cart.
                guest.UserId = userId;
                guest.GuestTokenHash = null;
                guest.UpdatedAt = now;
                foreach (var item in guestItems)
                    if (CheckLine(item.Product, item.Quantity) is { } conflict) conflicts.Add(conflict);
                await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                DeleteGuestCookie(context);
                return Results.Ok(new CartMergeDto(await LoadAsync(db, guest.Id, cancellationToken), true, conflicts));
            }

            var customerItems = await db.CartItems.Where(item => item.CartId == customer.Id).ToListAsync(cancellationToken);
            var leftOver = false;
            foreach (var item in guestItems)
            {
                var existing = customerItems.FirstOrDefault(line => line.ProductId == item.ProductId);
                int quantity;
                if (existing is not null)
                {
                    // Same product: add the quantities. More than the stock is kept and reported, not cut.
                    existing.Quantity += item.Quantity;
                    quantity = existing.Quantity;
                }
                else if (customerItems.Count >= MaxLines)
                {
                    leftOver = true;
                    conflicts.Add(new CartMergeConflictDto(
                        item.ProductId, item.Product.Name, "CartFull", item.Quantity, item.Product.StockQuantity));
                    continue;
                }
                else
                {
                    var moved = new CartItem { CartId = customer.Id, ProductId = item.ProductId, Quantity = item.Quantity };
                    db.CartItems.Add(moved);
                    customerItems.Add(moved);
                    quantity = item.Quantity;
                }
                db.CartItems.Remove(item);
                if (CheckLine(item.Product, quantity) is { } conflict) conflicts.Add(conflict);
            }

            customer.UpdatedAt = now;
            guest.UpdatedAt = now;
            // A guest cart keeps its token hash (every cart needs an owner), but an Abandoned cart is never found again.
            if (!leftOver) guest.State = CartState.Abandoned;
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            if (!leftOver) DeleteGuestCookie(context);
            return Results.Ok(new CartMergeDto(await LoadAsync(db, customer.Id, cancellationToken), true, conflicts));
        }).RequireAuthorization().AddEndpointFilter<CsrfFilter>();
    }

    // Guests and customers may use a cart; staff accounts are not shoppers.
    private static bool MayUseCart(HttpContext context) =>
        context.User.Identity?.IsAuthenticated != true || context.User.IsInRole(RolePolicies.Customer);

    private static IResult MergeRequired() => Results.Problem(statusCode: StatusCodes.Status409Conflict,
        title: "Your carts need to be merged.", detail: "More than one open cart belongs to this account.");

    private static IResult CartChanged() => Results.Problem(statusCode: StatusCodes.Status409Conflict,
        title: "Your cart changed.", detail: "Reload the cart and try again.");

    private static IResult NotForStaff() => Results.Problem(statusCode: StatusCodes.Status403Forbidden,
        title: "Only guests and customers have a shopping cart.");

    // Looks the owner's Open cart up without tracking it; writers then lock it with LockCartAsync.
    // A customer with more than one Open cart must merge them first, as checkout also requires.
    private static async Task<(Cart? Cart, bool MergeRequired)> FindCartAsync(
        HttpContext context, StoreDbContext db, CancellationToken cancellationToken)
    {
        var open = db.Carts.AsNoTracking().Where(cart => cart.State == CartState.Open);
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var carts = await open.Where(cart => cart.UserId == userId).Take(2).ToListAsync(cancellationToken);
            return (carts.FirstOrDefault(), carts.Count > 1);
        }

        var token = context.Request.Cookies[GuestCookie];
        if (!IsValidToken(token)) return (null, false);
        var hash = HashToken(token!);
        return (await open.FirstOrDefaultAsync(cart => cart.GuestTokenHash == hash, cancellationToken), false);
    }

    // On MySQL a cart changes in a ReadCommitted transaction that locks the customer row (if any)
    // and then the cart row, the same order checkout uses. The in-memory test database has no
    // transactions or row locks, so there the guard is skipped.
    private static async Task<IDbContextTransaction?> BeginAsync(
        HttpContext context, StoreDbContext db, CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational()) return null;
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await db.Users.FromSqlInterpolated($"SELECT * FROM Users WHERE Id = {userId} FOR UPDATE")
                .AsNoTracking().ToListAsync(cancellationToken);
        }
        return transaction;
    }

    // Returns the tracked cart (locked on MySQL), or null when it is gone or no longer Open.
    private static async Task<Cart?> LockCartAsync(StoreDbContext db, int cartId, CancellationToken cancellationToken)
    {
        var cart = db.Database.IsRelational()
            ? (await db.Carts.FromSqlInterpolated($"SELECT * FROM Carts WHERE Id = {cartId} FOR UPDATE")
                .ToListAsync(cancellationToken)).SingleOrDefault()
            : await db.Carts.FirstOrDefaultAsync(c => c.Id == cartId, cancellationToken);
        return cart is { State: CartState.Open } ? cart : null;
    }

    // A new guest cart always gets a fresh token, so an old or tampered cookie is never reused.
    private static Cart CreateCart(HttpContext context)
    {
        var cart = new Cart();
        if (context.User.Identity?.IsAuthenticated == true)
        {
            cart.UserId = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return cart;
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        cart.GuestTokenHash = HashToken(token);
        // Same rule as the account cookies: Secure everywhere except plain-HTTP development.
        var development = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
        context.Response.Cookies.Append(GuestCookie, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = !development || context.Request.IsHttps,
            MaxAge = GuestCartLifetime,
            IsEssential = true,
            Path = "/"
        });
        return cart;
    }

    // Tells the customer about a line the cart cannot sell as it is. The checkout checks the stock again.
    private static CartMergeConflictDto? CheckLine(Product product, int quantity)
    {
        if (!product.IsActive)
            return new CartMergeConflictDto(product.Id, product.Name, "Unavailable", quantity, product.StockQuantity);
        return quantity > product.StockQuantity
            ? new CartMergeConflictDto(product.Id, product.Name, "NotEnoughStock", quantity, product.StockQuantity)
            : null;
    }

    private static void DeleteGuestCookie(HttpContext context) =>
        context.Response.Cookies.Delete(GuestCookie, new CookieOptions { Path = "/" });

    private static bool IsValidToken(string? token) =>
        token is { Length: 64 } && token.All(Uri.IsHexDigit);

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)));

    private static async Task<CartDto> LoadAsync(StoreDbContext db, int cartId, CancellationToken cancellationToken)
    {
        var rows = await db.CartItems.AsNoTracking()
            .Where(item => item.CartId == cartId)
            .OrderBy(item => item.Id)
            .Select(item => new
            {
                item.ProductId, item.Product.Name, item.Product.Price, item.Quantity,
                item.Product.StockQuantity, item.Product.ImageUrl, item.Product.IsActive
            })
            .ToListAsync(cancellationToken);

        var lines = rows.Select(row => new CartLineDto(
            row.ProductId, row.Name, row.Price, row.Quantity, row.StockQuantity, row.ImageUrl,
            row.Price * row.Quantity, row.IsActive && row.StockQuantity >= row.Quantity)).ToList();
        return CartDto.FromLines(lines);
    }

    // Cart responses are private: never cached, and a database outage becomes a safe 503.
    private static async ValueTask<object?> PrivateRequest(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try { return await next(context); }
        catch (Exception exception) when (DatabaseErrors.IsUnavailable(exception))
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The cart service is temporarily unavailable.", detail: "Please try again shortly.");
        }
    }
}
