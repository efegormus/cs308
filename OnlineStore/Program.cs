using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using OnlineStore.Data;
using OnlineStore.Catalog;
using OnlineStore.Seeding;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Set ConnectionStrings:DefaultConnection using User Secrets or the ConnectionStrings__DefaultConnection environment variable.");
}

if (!Version.TryParse(builder.Configuration["MySql:ServerVersion"], out var serverVersion)
    || serverVersion < new Version(8, 0, 16))
{
    throw new InvalidOperationException("MySql:ServerVersion must be a MySQL version of 8.0.16 or later.");
}

MySqlConnectionStringBuilder connectionOptions;
try
{
    connectionOptions = new MySqlConnectionStringBuilder(connectionString)
    {
        DateTimeKind = MySqlDateTimeKind.Utc
    };
}
catch (Exception exception) when (exception is ArgumentException or FormatException)
{
    // Do not include the supplied connection string or parser error in logs.
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be a valid MySQL connection string.");
}

if (string.IsNullOrWhiteSpace(connectionOptions.Database))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection must specify a database.");
}

builder.Services.AddDbContext<StoreDbContext>(options =>
    options.UseMySql(connectionOptions.ConnectionString, new MySqlServerVersion(serverVersion)));

var app = builder.Build();

// `dotnet run --project OnlineStore -- --seed` loads the sample data and exits (development only).
if (args.Contains(SeedCommand.Flag))
{
    Environment.ExitCode = await SeedCommand.RunAsync(app);
    return;
}

// Serve the plain HTML/CSS/JS front end from the wwwroot folder.
app.UseDefaultFiles();
app.UseStaticFiles();

// Simple endpoint to check that the API is running.
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapCatalogEndpoints();
app.MapProductEndpoints();

app.Run();

// Allows integration tests to host the application in process.
public partial class Program { }
