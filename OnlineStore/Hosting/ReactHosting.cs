using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;

namespace OnlineStore.Hosting;

public static class ReactHosting
{
    public static void AddReactHosting(this IServiceCollection services) =>
        services.Configure<RouteOptions>(options => options.ConstraintMap["spa"] = typeof(SpaPathConstraint));

    public static void UseReactHosting(this WebApplication app)
    {
        var configured = app.Configuration["Frontend:DistPath"];
        var published = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "spa");
        var root = Path.GetFullPath(!string.IsNullOrWhiteSpace(configured) ? configured
            : Directory.Exists(published) ? published
            : Path.Combine(app.Environment.ContentRootPath, "..", "frontend", "dist"));
        if (Directory.Exists(root))
        {
            var files = new PhysicalFileProvider(root);
            app.Lifetime.ApplicationStopped.Register(files.Dispose);
            var options = new StaticFileOptions
            {
                FileProvider = files,
                OnPrepareResponse = context =>
                {
                    // Navigation must discover the latest hashed bundle after a deployment.
                    context.Context.Response.Headers.CacheControl = context.File.Name == "index.html"
                        ? "no-cache" : "public,max-age=3600";
                }
            };
            app.UseStaticFiles(options);
            app.MapFallbackToFile("/{*path:nonfile:spa}", "index.html", options).AllowAnonymous();
        }
        else
        {
            app.Logger.LogWarning("React build missing. Run npm ci and npm run build in frontend before starting the storefront.");
            app.MapFallback("/{*path:nonfile:spa}", () => Results.Problem(statusCode: 503,
                title: "The storefront is temporarily unavailable.")).AllowAnonymous();
        }

        // There is deliberately no UseDefaultFiles: / must use the React fallback.
        app.UseStaticFiles();
        app.UseRouting();
        app.Use(async (context, next) =>
        {
            // Unknown API paths and missing assets must never receive index.html or an auth challenge.
            if (context.GetEndpoint() is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            await next(context);
        });
    }
}

public sealed class SpaPathConstraint : IRouteConstraint
{
    public bool Match(HttpContext? context, IRouter? route, string key, RouteValueDictionary values,
        RouteDirection direction) => context is not null
        && !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && !context.Request.Path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase);
}
