using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using OnlineStore.Data;
using OnlineStore.Catalog;
using OnlineStore.Seeding;
using OnlineStore.Accounts;
using OnlineStore.Invoicing;
using OnlineStore.Purchasing;
using OnlineStore.Shopping;
using OnlineStore.Errors;
using OnlineStore.Hosting;
using OnlineStore.Dashboards;

var builder = WebApplication.CreateBuilder(args);

// The middleware logs full exceptions; ApiExceptionHandler provides sanitized diagnostics.
builder.Logging.AddFilter(
    "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);

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
builder.AddStoreAccounts();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICheckoutPaymentGateway, MockPaymentGateway>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<DeliveryService>();
// Invoice PDFs live outside wwwroot (SCRUM-74).
var invoiceRoot = builder.Configuration["Invoices:StoragePath"];
builder.Services.AddSingleton<IInvoiceStorage>(new FileInvoiceStorage(string.IsNullOrWhiteSpace(invoiceRoot)
    ? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "invoices") : invoiceRoot));
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<InvoiceEmailOutbox>();
builder.Services.AddScoped<InvoiceEmailDispatcher>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
var emailTransport = builder.Configuration["Email:Transport"] ?? (builder.Environment.IsDevelopment() ? "Local" : "Smtp");
if (emailTransport.Equals("Local", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<LocalInvoiceEmailSender>();
    builder.Services.AddSingleton<IInvoiceEmailSender>(s => s.GetRequiredService<LocalInvoiceEmailSender>());
}
else if (emailTransport.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<IInvoiceEmailSender, SmtpInvoiceEmailSender>();
else throw new InvalidOperationException("Email:Transport must be Local or Smtp.");
builder.Services.AddHostedService<InvoiceEmailBackgroundService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddReactHosting();

var app = builder.Build();

if (args.Contains(RoleCommand.Flag))
{
    Environment.ExitCode = await RoleCommand.RunAsync(app, args);
    return;
}

if (args.Contains(DemoSeedCommand.Flag))
{
    Environment.ExitCode = await DemoSeedCommand.RunAsync(app);
    return;
}

// `dotnet run --project OnlineStore -- --seed` loads the sample data and exits (development only).
if (args.Contains(SeedCommand.Flag))
{
    Environment.ExitCode = await SeedCommand.RunAsync(app);
    return;
}

app.UseExceptionHandler();
app.UseReactHosting();
app.UsePrivateResponseNoStore();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Simple endpoint to check that the API is running.
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapCatalogEndpoints();
app.MapProductEndpoints();
app.MapAccountEndpoints();
app.MapPurchaseEndpoints();
app.MapCartEndpoints();
app.MapWishlistEndpoints();
app.MapInvoiceEndpoints();
app.MapDashboardEndpoints();

app.Run();

// Allows integration tests to host the application in process.
public partial class Program { }
