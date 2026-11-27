using Microsoft.AspNetCore.SignalR;
using System.Security.Cryptography;
using OnlineStore.Accounts;
using OnlineStore.Purchasing;

namespace OnlineStore.Support;

public static class ChatEndpoints
{
    public static void MapSupportChat(this WebApplication app)
    {
        app.MapPost("/api/chat/conversations", async (HttpContext http, ChatService service, IHubContext<SupportChatHub> hub, CancellationToken ct) =>
        {
            var token = http.Request.Cookies["OnlineStore.Chat"];
            if (token is not { Length: 64 }) token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var view = await service.StartAsync(ChatService.Actor(http.User, token), ct);
            http.Response.Cookies.Append("OnlineStore.Chat", token, new CookieOptions
            { HttpOnly = true, Secure = http.Request.IsHttps, SameSite = SameSiteMode.Strict, MaxAge = TimeSpan.FromHours(24), Path = "/" });
            await hub.Clients.Group("support:queue").SendAsync("QueueChanged", ct);
            return Results.Created($"/api/chat/conversations/{view.Id}", view);
        }).AllowAnonymous().RequireRateLimiting("chat-start").AddEndpointFilter<CsrfFilter>().AddEndpointFilter<PurchaseErrorFilter>();
        app.MapHub<SupportChatHub>("/hubs/support", options => options.CloseOnAuthenticationExpiration = true).AllowAnonymous();
    }
}
