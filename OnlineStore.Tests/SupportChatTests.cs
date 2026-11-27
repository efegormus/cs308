using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;
using OnlineStore.Support;

namespace OnlineStore.Tests;

public class SupportChatTests
{
    private static StoreDbContext Db() => new(new DbContextOptionsBuilder<StoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<User> Agent(StoreDbContext db)
    {
        var u = new User { Name = "Agent", Email = "agent@example.test", PasswordHash = "unused", Role = UserRole.SupportAgent };
        db.Users.Add(u); await db.SaveChangesAsync(); return u;
    }
    [Fact] public async Task GuestCanQueueAndResumeButAnotherGuestCannotRead()
    {
        await using var db = Db(); var s = new ChatService(db, TimeProvider.System);
        var guest = new ChatActor(null, ChatService.Hash(new string('a',64)));
        var c = await s.StartAsync(guest,default); await s.SendAsync(guest,c.Id," hello ",default);
        Assert.Equal("hello",Assert.Single((await s.HistoryAsync(guest,c.Id,0,default)).Messages).Body);
        Assert.Equal(404,(await Assert.ThrowsAsync<PurchaseException>(() => s.AccessAsync(new(null,"other"),c.Id,default))).Status);
    }
    [Fact] public async Task QueueIsOldestFirstAndOnlyAgentsCanClaim()
    {
        await using var db = Db(); var s = new ChatService(db,TimeProvider.System); var a = await Agent(db);
        var guest = new ChatActor(null,"guest"); var first = await s.StartAsync(guest,default); var second = await s.StartAsync(guest,default);
        Assert.Equal(403,(await Assert.ThrowsAsync<PurchaseException>(()=>s.QueueAsync(guest,0,default))).Status);
        Assert.Equal(new[]{first.Id,second.Id},(await s.QueueAsync(new(a.Id,null),0,default)).Select(c=>c.Id));
        await s.ClaimAsync(new(a.Id,null),first.Id,default);
        Assert.Equal(second.Id,Assert.Single(await s.QueueAsync(new(a.Id,null),0,default)).Id);
    }
    [Fact] public async Task ClaimIsIdempotentForOwnerButRejectsSecondAgent()
    {
        await using var db = Db(); var a = await Agent(db); var b = new User{Name="Second",Email="second@example.test",PasswordHash="unused",Role=UserRole.SupportAgent};db.Users.Add(b);await db.SaveChangesAsync();
        var s = new ChatService(db,TimeProvider.System);var c=await s.StartAsync(new(null,"guest"),default);
        await s.ClaimAsync(new(a.Id,null),c.Id,default);await s.ClaimAsync(new(a.Id,null),c.Id,default);
        Assert.Equal(409,(await Assert.ThrowsAsync<PurchaseException>(()=>s.ClaimAsync(new(b.Id,null),c.Id,default))).Status);
        Assert.Equal(404,(await Assert.ThrowsAsync<PurchaseException>(()=>s.HistoryAsync(new(b.Id,null),c.Id,0,default))).Status);
    }
    [Theory][InlineData(null)][InlineData("")][InlineData("   ")]
    public async Task EmptyMessagesCannotBeStored(string? body)
    {
        await using var db=Db();var s=new ChatService(db,TimeProvider.System);
        Assert.Equal(400,(await Assert.ThrowsAsync<PurchaseException>(()=>s.SendAsync(new(null,"g"),1,body,default))).Status);Assert.Empty(db.ChatMessages);
    }
    [Fact] public async Task LongMessagesAndClosedConversationsAreRejected()
    {
        await using var db=Db();var s=new ChatService(db,TimeProvider.System);var g=new ChatActor(null,"g");var c=await s.StartAsync(g,default);
        await Assert.ThrowsAsync<PurchaseException>(()=>s.SendAsync(g,c.Id,new string('x',2001),default));
        var row=await db.Conversations.SingleAsync();row.Status=ConversationStatus.Closed;row.ClosedAt=DateTime.UtcNow;await db.SaveChangesAsync();
        Assert.Equal(409,(await Assert.ThrowsAsync<PurchaseException>(()=>s.SendAsync(g,c.Id,"closed",default))).Status);
    }
    [Fact] public async Task ContextRequiresAssignedAgentAndDoesNotExposePrivateIdentity()
    {
        await using var db=Db();var fixture=await ReviewFixture.SeedAsync(db);var a=await Agent(db);var s=new ChatService(db,TimeProvider.System);
        var c=await s.StartAsync(new(fixture.Buyer.Id,null),default);
        Assert.Equal(404,(await Assert.ThrowsAsync<PurchaseException>(()=>s.ContextAsync(new(a.Id,null),c.Id,default))).Status);
        await s.ClaimAsync(new(a.Id,null),c.Id,default);
        var json=System.Text.Json.JsonSerializer.Serialize(await s.ContextAsync(new(a.Id,null),c.Id,default));
        Assert.Contains("orders",json);Assert.DoesNotContain(fixture.Buyer.Email,json);Assert.DoesNotContain("PasswordHash",json);
        Assert.Equal(403,(await Assert.ThrowsAsync<PurchaseException>(()=>s.ContextAsync(new(fixture.Buyer.Id,null),c.Id,default))).Status);
    }
}
