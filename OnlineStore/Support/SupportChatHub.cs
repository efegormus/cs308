using Microsoft.AspNetCore.SignalR;
using OnlineStore.Purchasing;

namespace OnlineStore.Support;

// Room membership is never caller-selected without checking persistent ownership/assignment.
public sealed class SupportChatHub(ChatService service) : Hub
{
    private ChatActor Actor => ChatService.Actor(Context.User!, Context.GetHttpContext()!.Request.Cookies["OnlineStore.Chat"]);
    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext()!;
        if (http.Request.Headers.Origin.Count > 0 && (!Uri.TryCreate(http.Request.Headers.Origin.ToString(), UriKind.Absolute, out var origin)
            || origin.Authority != http.Request.Host.Value || origin.Scheme != http.Request.Scheme))
        { Context.Abort(); return; }
        await base.OnConnectedAsync();
    }
    private static string Room(long id) => $"chat:{id}";
    private async Task<T> Run<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (PurchaseException e) { throw new HubException($"{e.Status}: {e.Message}"); }
    }
    public Task WatchQueue() => Run(async () =>
    { await service.RequireAgentAsync(Actor, Context.ConnectionAborted); await Groups.AddToGroupAsync(Context.ConnectionId, "support:queue"); return true; });
    public Task Join(long id) => Run(async () =>
    { await service.AccessAsync(Actor, id, Context.ConnectionAborted); await Groups.AddToGroupAsync(Context.ConnectionId, Room(id)); return true; });
    public Task<IReadOnlyList<ChatView>> Queue(long afterId = 0) => Run(() => service.QueueAsync(Actor, afterId, Context.ConnectionAborted));
    public Task<MessagePage> History(long id, long afterId = 0) => Run(() => service.HistoryAsync(Actor, id, afterId, Context.ConnectionAborted));
    public Task<object> CustomerContext(long id) => Run(() => service.ContextAsync(Actor, id, Context.ConnectionAborted));
    public Task<ChatView> Claim(long id) => Run(async () =>
    {
        var result = await service.ClaimAsync(Actor, id, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, Room(id));
        await Clients.Group(Room(id)).SendAsync("Claimed", result);
        await Clients.Group("support:queue").SendAsync("QueueChanged"); return result;
    });
    public Task<MessageView> Send(long id, string body) => Run(async () =>
    {
        var message = await service.SendAsync(Actor, id, body, Context.ConnectionAborted);
        await Clients.Group(Room(id)).SendAsync("Message", id, message); return message;
    });
}
