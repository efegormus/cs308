using Microsoft.EntityFrameworkCore;
using OnlineStore.Models;
using OnlineStore.Purchasing;
using OnlineStore.Support;

namespace OnlineStore.Tests;

[Collection("MySql")]
public class SupportChatMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    [MySqlFact] public async Task MySql_ConcurrentClaimsHaveOneWinnerAndMessagesPersist()
    {
        await fixture.ResetAsync();long id;long first;long second;
        await using(var db=fixture.Create())
        {
            var a=new User{Name="Agent A",Email="a@example.test",PasswordHash="unused",Role=UserRole.SupportAgent};
            var b=new User{Name="Agent B",Email="b@example.test",PasswordHash="unused",Role=UserRole.SupportAgent};db.Users.AddRange(a,b);await db.SaveChangesAsync();first=a.Id;second=b.Id;
            id=(await new ChatService(db,TimeProvider.System).StartAsync(new(null,"guest-hash"),default)).Id;
        }
        async Task<int> Claim(long agent)
        {
            await using var db=fixture.Create();try{await new ChatService(db,TimeProvider.System).ClaimAsync(new(agent,null),id,default);return 200;}catch(PurchaseException e){return e.Status;}
        }
        Assert.Equal(new[]{200,409},(await Task.WhenAll(Claim(first),Claim(second))).Order());
        await using var check=fixture.Create();var row=await check.Conversations.SingleAsync();Assert.Equal(ConversationStatus.Active,row.Status);Assert.NotNull(row.ClaimedAt);
        var service=new ChatService(check,TimeProvider.System);await service.SendAsync(new(null,"guest-hash"),id,"guest message",default);
        await service.SendAsync(new(row.ClaimedByUserId,null),id,"agent reply",default);
        Assert.Equal(new[]{"Guest","Agent"},(await service.HistoryAsync(new(null,"guest-hash"),id,0,default)).Messages.Select(m=>m.Sender));
        Assert.Equal("agent reply",Assert.Single((await service.HistoryAsync(new(null,"guest-hash"),id,(await check.ChatMessages.OrderBy(m=>m.Id).FirstAsync()).Id,default)).Messages).Body);
        Assert.NotNull(await service.ContextAsync(new(row.ClaimedByUserId,null),id,default));
    }
}
