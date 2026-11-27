using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OnlineStore.Data;

namespace OnlineStore.Tests;

[Collection("MySql")]
public class SupportChatHubMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    [MySqlFact] public async Task MySql_GuestSocketReceivesPersistedMessagesAndCannotJoinAnotherRoom()
    {
        await fixture.ResetAsync();using var app=new ChatApplication(fixture);using var http=app.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest,(await http.PostAsync("/api/chat/conversations",null)).StatusCode);
        var csrf=await http.GetFromJsonAsync<JsonElement>("/api/auth/csrf");using var request=new HttpRequestMessage(HttpMethod.Post,"/api/chat/conversations");request.Headers.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());
        var result=await http.SendAsync(request);Assert.Equal(HttpStatusCode.Created,result.StatusCode);
        var id=(await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        var cookie=result.Headers.GetValues("Set-Cookie").Single(s=>s.StartsWith("OnlineStore.Chat=")).Split(';')[0];
        var client=app.Server.CreateWebSocketClient();client.ConfigureRequest=r=>r.Headers.Cookie=cookie;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));using var socket=await client.ConnectAsync(new Uri("ws://localhost/hubs/support"),timeout.Token);
        async Task Send(string data)=>await socket.SendAsync(Encoding.UTF8.GetBytes(data+'\u001e'),WebSocketMessageType.Text,true,timeout.Token);
        async Task<string> Receive(){var b=new byte[8192];var r=await socket.ReceiveAsync(b,timeout.Token);return Encoding.UTF8.GetString(b,0,r.Count);}
        await Send("{\"protocol\":\"json\",\"version\":1}");Assert.Contains("{}",await Receive());
        await Send(JsonSerializer.Serialize(new{type=1,invocationId="join",target="Join",arguments=new object[]{id}}));Assert.Contains("join",await Receive());
        await Send(JsonSerializer.Serialize(new{type=1,invocationId="send",target="Send",arguments=new object[]{id,"Socket message"}}));
        var packets="";while(!packets.Contains("\"type\":3"))packets+=await Receive();Assert.Contains("Socket message",packets);Assert.Contains("\"target\":\"Message\"",packets);
        await Send(JsonSerializer.Serialize(new{type=1,invocationId="denied",target="Join",arguments=new object[]{id+1}}));Assert.Contains("404",await Receive());
        await using var db=fixture.Create();Assert.Equal("Socket message",Assert.Single(db.ChatMessages).Body);
    }
    private sealed class ChatApplication(MySqlFixture fixture):WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");builder.UseSetting("MySql:ServerVersion","8.4.13");builder.UseSetting("Email:Transport","Local");builder.UseSetting("ConnectionStrings:DefaultConnection","Server=localhost;Database=unused;User ID=unused;Password=unused");
            builder.ConfigureLogging(l=>l.ClearProviders());builder.ConfigureServices(s=>{s.RemoveAll<StoreDbContext>();s.AddScoped(_=>fixture.Create());});
        }
    }
}
