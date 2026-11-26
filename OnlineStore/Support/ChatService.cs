using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Support;

public sealed record ChatActor(long? UserId, string? GuestHash);
public sealed record ChatView(long Id, string Status, long? AgentId, DateTime CreatedAt);
public sealed record MessageView(long Id, string Sender, string Body, DateTime SentAt);
public sealed record MessagePage(IReadOnlyList<MessageView> Messages, long? NextAfterId);

public sealed class ChatService(StoreDbContext db, TimeProvider clock)
{
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public static ChatActor Actor(ClaimsPrincipal user, string? token) => new(
        long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null,
        token is { Length: 64 } ? Hash(token) : null);
    private async Task<User?> UserAsync(ChatActor actor, CancellationToken ct) => actor.UserId is { } id
        ? await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id && u.IsActive, ct) : null;
    public async Task RequireAgentAsync(ChatActor actor, CancellationToken ct)
    {
        if ((await UserAsync(actor, ct))?.Role != UserRole.SupportAgent) throw new PurchaseException(403, "A support agent is required.");
    }
    public async Task<ChatView> StartAsync(ChatActor actor, CancellationToken ct)
    {
        if (actor.UserId is not null && (await UserAsync(actor, ct))?.Role != UserRole.Customer)
            throw new PurchaseException(403, "A customer account is required.");
        if (actor.UserId is null && actor.GuestHash is null) throw new PurchaseException(401, "Start a guest session first.");
        var conversation = new Conversation { CustomerUserId = actor.UserId,
            GuestTokenHash = actor.UserId is null ? actor.GuestHash : null, CreatedAt = Now() };
        db.Conversations.Add(conversation); await db.SaveChangesAsync(ct); return View(conversation);
    }
    public async Task<IReadOnlyList<ChatView>> QueueAsync(ChatActor actor, long afterId, CancellationToken ct)
    {
        await RequireAgentAsync(actor, ct);
        if (afterId < 0) throw new PurchaseException(400, "Invalid cursor.");
        return await db.Conversations.AsNoTracking().Where(c => c.Status == ConversationStatus.Waiting && c.Id > afterId)
            .OrderBy(c => c.Id).Take(50).Select(c => new ChatView(c.Id, "Waiting", c.ClaimedByUserId, c.CreatedAt)).ToListAsync(ct);
    }
    public async Task<Conversation> AccessAsync(ChatActor actor, long id, CancellationToken ct)
    {
        var c = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new PurchaseException(404, "Conversation not found.");
        var user = await UserAsync(actor, ct);
        var owner = actor.UserId is not null ? user?.Role == UserRole.Customer && c.CustomerUserId == actor.UserId
            : c.CustomerUserId is null && actor.GuestHash is not null && c.GuestTokenHash == actor.GuestHash;
        if (!owner && !(user?.Role == UserRole.SupportAgent && c.ClaimedByUserId == user.Id))
            throw new PurchaseException(404, "Conversation not found.");
        return c;
    }
    public async Task<ChatView> ClaimAsync(ChatActor actor, long id, CancellationToken ct)
    {
        await RequireAgentAsync(actor, ct);
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var c = db.Database.IsRelational()
            ? (await db.Conversations.FromSqlInterpolated($"SELECT * FROM Conversations WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            : await db.Conversations.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (c is null) throw new PurchaseException(404, "Conversation not found.");
        if (c.Status == ConversationStatus.Active && c.ClaimedByUserId == actor.UserId) return View(c);
        if (c.Status != ConversationStatus.Waiting) throw new PurchaseException(409, "Conversation already claimed or closed.");
        c.Status = ConversationStatus.Active; c.ClaimedByUserId = actor.UserId; c.ClaimedAt = Now();
        await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct); return View(c);
    }
    public async Task<MessageView> SendAsync(ChatActor actor, long id, string? body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 2000) throw new PurchaseException(400, "Use 1–2000 characters.");
        var c = await AccessAsync(actor, id, ct);
        if (c.Status == ConversationStatus.Closed) throw new PurchaseException(409, "Conversation is closed.");
        var kind = actor.UserId is null ? "Guest" : c.ClaimedByUserId == actor.UserId ? "Agent" : "Customer";
        var message = new ChatMessage { ConversationId = id, SenderUserId = actor.UserId, SenderKind = kind, Body = body.Trim(), SentAt = Now() };
        db.ChatMessages.Add(message); await db.SaveChangesAsync(ct); return new(message.Id, kind, message.Body, message.SentAt);
    }
    public async Task<MessagePage> HistoryAsync(ChatActor actor, long id, long afterId, CancellationToken ct)
    {
        await AccessAsync(actor, id, ct);
        if (afterId < 0) throw new PurchaseException(400, "Invalid cursor.");
        var rows = await db.ChatMessages.AsNoTracking().Where(m => m.ConversationId == id && m.Id > afterId).OrderBy(m => m.Id).Take(51)
            .Select(m => new MessageView(m.Id, m.SenderKind, m.Body ?? "", m.SentAt)).ToListAsync(ct);
        return new(rows.Take(50).ToList(), rows.Count > 50 ? rows[49].Id : null);
    }
    public async Task<object> ContextAsync(ChatActor actor, long id, CancellationToken ct)
    {
        await RequireAgentAsync(actor, ct); var conversation = await AccessAsync(actor, id, ct);
        if (conversation.CustomerUserId is not { } owner) return new { customerId = (long?)null };
        var cart = await db.CartItems.AsNoTracking().Where(i => i.Cart.UserId == owner && i.Cart.State == CartState.Open)
            .OrderBy(i => i.Id).Take(100).Select(i => new { i.ProductId, Name = i.Product.Name, i.Quantity }).ToListAsync(ct);
        var orders = await db.Orders.AsNoTracking().Where(o => o.UserId == owner).OrderByDescending(o => o.Id).Take(20)
            .Select(o => new { o.Id, Status = o.Status.ToString(), o.GrandTotal }).ToListAsync(ct);
        var wishlist = await db.WishlistItems.AsNoTracking().Where(i => i.UserId == owner).OrderBy(i => i.ProductId).Take(100)
            .Select(i => new { i.ProductId, Name = i.Product.Name }).ToListAsync(ct);
        return new { customerId = owner, cart, orders, wishlist };
    }
    private DateTime Now() { var now = clock.GetUtcNow().UtcDateTime; return now.AddTicks(-(now.Ticks % 10)); }
    private static ChatView View(Conversation c) => new(c.Id, c.Status.ToString(), c.ClaimedByUserId, c.CreatedAt);
}
