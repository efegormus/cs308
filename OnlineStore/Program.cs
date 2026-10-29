var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Serve the plain HTML/CSS/JS front end from the wwwroot folder.
app.UseDefaultFiles();
app.UseStaticFiles();

// Simple endpoint to check that the API is running.
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();
