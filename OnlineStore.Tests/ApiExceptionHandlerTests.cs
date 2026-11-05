
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OnlineStore.Errors;

namespace OnlineStore.Tests;

public class ApiExceptionHandlerTests
{
    [Fact]
    public async Task ValidationException_ReturnsStructured400()
    {
        using var services = CreateServices();
        var (context, body) = CreateContext(services);
        using (body)
        {
            var handler = new ApiExceptionHandler(
                NullLogger<ApiExceptionHandler>.Instance);

            var handled = await handler.TryHandleAsync(
                context,
                new ValidationException("Sensitive submitted value"),
                CancellationToken.None);

            Assert.True(handled);
            Assert.Equal(400, context.Response.StatusCode);

            using var json = await ReadJson(body);
            Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal(
                "Request validation failed.",
                json.RootElement.GetProperty("title").GetString());
            Assert.Equal(
                "scrum60-test-trace",
                json.RootElement.GetProperty("traceId").GetString());

            Assert.DoesNotContain(
                "Sensitive submitted value",
                json.RootElement.ToString());
        }
    }

    [Theory]
    [InlineData(400)]
    [InlineData(413)]
    public async Task BadHttpRequest_PreservesOriginalStatus(int statusCode)
    {
        using var services = CreateServices();
        var (context, body) = CreateContext(services);
        using (body)
        {
            var handler = new ApiExceptionHandler(
                NullLogger<ApiExceptionHandler>.Instance);

            var handled = await handler.TryHandleAsync(
                context,
                new BadHttpRequestException("Sensitive HTTP details", statusCode),
                CancellationToken.None);

            Assert.True(handled);
            Assert.Equal(statusCode, context.Response.StatusCode);

            using var json = await ReadJson(body);
            Assert.Equal(
                statusCode,
                json.RootElement.GetProperty("status").GetInt32());

            Assert.DoesNotContain(
                "Sensitive HTTP details",
                json.RootElement.ToString());
        }
    }

    [Fact]
    public async Task UnexpectedException_ReturnsSafe500()
    {
        using var services = CreateServices();
        var (context, body) = CreateContext(services);
        using (body)
        {
            var handler = new ApiExceptionHandler(
                NullLogger<ApiExceptionHandler>.Instance);

            var handled = await handler.TryHandleAsync(
                context,
                new InvalidOperationException(
                    "SECRET_PASSWORD SQL internal database details"),
                CancellationToken.None);

            Assert.True(handled);
            Assert.Equal(500, context.Response.StatusCode);

            using var json = await ReadJson(body);
            var response = json.RootElement.ToString();

            Assert.Equal(
                "Internal server error.",
                json.RootElement.GetProperty("title").GetString());

            Assert.DoesNotContain("SECRET_PASSWORD", response);
            Assert.DoesNotContain("SQL internal", response);
            Assert.Contains("scrum60-test-trace", response);
        }
    }

    private static ServiceProvider CreateServices()
    {
        return new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
    }

    private static (DefaultHttpContext, MemoryStream) CreateContext(
        IServiceProvider services)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            TraceIdentifier = "scrum60-test-trace"
        };

        var body = new MemoryStream();
        context.Response.Body = body;

        return (context, body);
    }

    private static async Task<JsonDocument> ReadJson(MemoryStream body)
    {
        body.Position = 0;
        return await JsonDocument.ParseAsync(body);
    }
}
