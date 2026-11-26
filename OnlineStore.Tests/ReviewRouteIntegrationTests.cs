using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OnlineStore.Data;

namespace OnlineStore.Tests;

// SCRUM-162: a textual merge can leave duplicate endpoints even when individual services compile.
public class ReviewRouteIntegrationTests
{
    [Fact] public async Task MergedModerationQueueHasOneGetRouteAndBothDecisionContracts()
    {
        using var app=new Application();using var client=app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/product-management/comments")).StatusCode);
        var endpoints=app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();
        var queue=endpoints.Where(e=>e.RoutePattern.RawText=="/api/product-management/comments"
            && e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("GET")).ToArray();
        Assert.Single(queue);
        Assert.Single(endpoints.Where(e=>e.RoutePattern.RawText=="/api/product-management/comments/{id:long}/moderation"));
        Assert.Single(endpoints.Where(e=>e.RoutePattern.RawText=="/api/product-management/comments/{commentId:long}/decision"));
    }
    private sealed class Application:WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");builder.UseSetting("MySql:ServerVersion","8.4.13");builder.UseSetting("Email:Transport","Local");
            builder.UseSetting("ConnectionStrings:DefaultConnection","Server=localhost;Database=unused;User ID=unused;Password=unused");builder.ConfigureLogging(l=>l.ClearProviders());
            var options=new DbContextOptionsBuilder<StoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            builder.ConfigureServices(s=>{s.RemoveAll<StoreDbContext>();s.AddScoped(_=>new StoreDbContext(options));});
        }
    }
}
