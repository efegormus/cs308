using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OnlineStore.Accounts;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Tests;

public class AccountTests
{
    private const string Password = "A long test passphrase!";
    private static RegisterRequest Registration => new("Customer", "customer@example.com", "TAX-123", "10 Example Street", Password);

    [Fact]
    public void Passwords_UseIndependentSaltsAndVerifyOnlyTheCorrectPassword()
    {
        var passwords = new PasswordService(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));
        var first = new User { Name = "one", Email = "one@example.com", PasswordHash = "" };
        var second = new User { Name = "two", Email = "two@example.com", PasswordHash = "" };
        passwords.SetPassword(first, Password);
        passwords.SetPassword(second, Password);
        Assert.NotEqual(Password, first.PasswordHash);
        Assert.NotEqual(first.PasswordHash, second.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, passwords.Verify(first, Password));
        Assert.Equal(PasswordVerificationResult.Failed, passwords.Verify(first, "wrong password"));
        first.PasswordHash = "not-an-encoded-password";
        Assert.Equal(PasswordVerificationResult.Failed, passwords.Verify(first, Password));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(11, false)]
    [InlineData(12, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Passwords_EnforceLengthBounds(int length, bool expected) =>
        Assert.Equal(expected, PasswordService.IsValidNewPassword(new string('x', length)));

    [Fact]
    public async Task Registration_StoresOnlyHashAndCompleteCustomerProfile()
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();
        var response = await PostAsync(client, "/api/auth/register", Registration);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var account = (await response.Content.ReadFromJsonAsync<AccountDto>())!;
        Assert.Equal("Customer", account.Role);
        Assert.DoesNotContain("password", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var scope = app.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<StoreDbContext>().Users.SingleAsync();
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.Equal("TAX-123", user.TaxId);
        Assert.Equal("10 Example Street", user.HomeAddress);
        Assert.Equal(UserRole.Customer, user.Role);
        Assert.NotEmpty(user.SecurityStamp);
    }


    [Fact]
    public async Task Registration_DoesNotSignInUntilLogin()
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();

        // 1. Yeni bir kullanıcı kaydediyoruz.
        var registration = await PostAsync(
            client, "/api/auth/register", Registration);

        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        // 2. Kayıt başarılı olsa bile kullanıcı henüz giriş yapmış olmamalı.
        var beforeLogin = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, beforeLogin.StatusCode);
        var anonymousCurrent = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCurrent.StatusCode);
        Assert.Equal("no-store", anonymousCurrent.Headers.CacheControl!.ToString());

        // 3. Şimdi doğru bilgilerle giriş yapıyoruz.
        var login = await PostAsync(
            client,
            "/api/auth/login",
            new LoginRequest(Registration.Email, Password));

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // 4. Girişten sonra oturumun aktif olduğunu doğruluyoruz.
        var afterLogin = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, afterLogin.StatusCode);

        var account = await afterLogin.Content.ReadFromJsonAsync<CurrentAccountDto>();

        Assert.NotNull(account);
        Assert.Equal(Registration.Email, account.Email);
        Assert.Equal(["Customer"], account.Roles);

        var current = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Equal("no-store", current.Headers.CacheControl!.ToString());
        var currentJson = await current.Content.ReadAsStringAsync();
        using var currentDocument = JsonDocument.Parse(currentJson);
        Assert.Equal(Registration.Email, currentDocument.RootElement.GetProperty("email").GetString());
        Assert.Equal("Customer", currentDocument.RootElement.GetProperty("role").GetString());
        Assert.Equal("Customer", currentDocument.RootElement.GetProperty("roles")[0].GetString());
        Assert.DoesNotContain("passwordHash", currentJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", currentJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("taxId", currentJson, StringComparison.OrdinalIgnoreCase);
    }




    [Fact]
    public async Task Registration_RejectsCaseInsensitiveDuplicateEmail()
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, "/api/auth/register", Registration)).StatusCode);
        var duplicate = await PostAsync(client, "/api/auth/register", Registration with { Email = "CUSTOMER@EXAMPLE.COM" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Registration_CannotAssignStaffRoleOrSupplyPasswordHash()
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();
        var response = await PostAsync(client, "/api/auth/register", new
        {
            Registration.Name, Registration.Email, Registration.TaxId, Registration.HomeAddress, Registration.Password,
            role = "ProductManager", passwordHash = "attacker-supplied-hash"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<StoreDbContext>().Users.ToListAsync());
    }

    [Theory]
    [InlineData("/api/auth/register")]
    [InlineData("/api/auth/login")]
    public async Task AnonymousMutations_RequireCsrfToken(string path)
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync(path, path.EndsWith("register") ? (object)Registration : new LoginRequest(Registration.Email, Password));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("anti-forgery", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Registration_RejectsIncompleteProfileAndWeakPassword()
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();
        var response = await PostAsync(client, "/api/auth/register", Registration with { TaxId = "", Password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("taxId", body);
        Assert.Contains("password", body);
    }

    [Fact]
    public async Task Login_ProfileAndLogout_UseAnOwnerOnlySession()
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/customer/profile")).StatusCode);
        await PostAsync(client, "/api/auth/register", Registration);
        var login = await PostAsync(client, "/api/auth/login", new LoginRequest("CUSTOMER@EXAMPLE.COM", Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), value => value.StartsWith("OnlineStore.Session=") && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        var profile = await client.GetFromJsonAsync<CustomerProfileDto>("/api/customer/profile?userId=999");
        Assert.Equal("customer@example.com", profile!.Email);
        Assert.Equal("TAX-123", profile.TaxId);
        Assert.Equal("10 Example Street", profile.HomeAddress);
        Assert.Equal("no-store", (await client.GetAsync("/api/auth/me")).Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/management/session")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(client, "/api/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task InvalidCredentials_AreGenericAndDoNotCreateSession()
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();
        await PostAsync(client, "/api/auth/register", Registration);
        var wrong = await PostAsync(client, "/api/auth/login", new LoginRequest(Registration.Email, "wrong"));
        var unknown = await PostAsync(client, "/api/auth/login", new LoginRequest("unknown@example.com", "wrong"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.SalesManager)]
    [InlineData(UserRole.ProductManager)]
    [InlineData(UserRole.SupportAgent)]
    public async Task TrustedRoleChange_RevokesOldSessionAndRestrictsCustomerProfile(UserRole role)
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();
        await PostAsync(client, "/api/auth/register", Registration);
        await PostAsync(client, "/api/auth/login", new LoginRequest(Registration.Email, Password));
        using (var scope = app.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<AccountService>().SetRoleAsync(Registration.Email!, role));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, "/api/auth/login", new LoginRequest(Registration.Email, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/management/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/customer/profile")).StatusCode);
        Assert.Equal(role.ToString(), (await client.GetFromJsonAsync<CurrentAccountDto>("/api/auth/me"))!.Role);
    }

    [Fact]
    public async Task PasswordChange_InvalidatesExistingSession()
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();
        await PostAsync(client, "/api/auth/register", Registration);
        await PostAsync(client, "/api/auth/login", new LoginRequest(Registration.Email, Password));
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            var user = await db.Users.SingleAsync();
            scope.ServiceProvider.GetRequiredService<PasswordService>().SetPassword(user, "A different long password!");
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(client, "/api/auth/login", new LoginRequest(Registration.Email, Password))).StatusCode);
    }

    [Fact]
    public async Task Login_DatabaseFailureReturnsSafe503()
    {
        using var app = new AccountApplication(useDatabase: true);
        using var client = app.CreateClient();
        var response = await PostAsync(client, "/api/auth/login", new LoginRequest(Registration.Email, Password));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("MySql", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_AttemptsAreRateLimited()
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();
        for (var index = 0; index < 10; index++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(client, "/api/auth/login", new LoginRequest("missing@example.com", "wrong"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostAsync(client, "/api/auth/login", new LoginRequest("missing@example.com", "wrong"))).StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesEvenAReplayedCookie()
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();
        await PostAsync(client, "/api/auth/register", Registration);
        var login = await PostAsync(client, "/api/auth/login", new LoginRequest(Registration.Email, Password));
        var oldCookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("OnlineStore.Session=")).Split(';')[0];
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(client, "/api/auth/logout", new { })).StatusCode);
        using var replay = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", oldCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task LegacyPasswordHash_IsUpgradedAfterSuccessfulVerification()
    {
        using var app = new AccountApplication();
        using var scope = app.Services.CreateScope();
        var user = new User { Name = "Legacy", Email = "legacy@example.com", PasswordHash = "" };
        user.PasswordHash = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions { IterationCount = 10_000 }))
            .HashPassword(user, "old-password");
        var oldHash = user.PasswordHash;
        var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var result = await scope.ServiceProvider.GetRequiredService<AccountService>().AuthenticateAsync(user.Email, "old-password");
        Assert.NotNull(result);
        Assert.NotEqual(oldHash, result.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, scope.ServiceProvider.GetRequiredService<PasswordService>()
            .Verify(result, "old-password"));
    }

    [Fact]
    public async Task ProductionCookies_AreSecureAndHttpOnly()
    {
        using var app = new AccountApplication(production: true);
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        await PostAsync(client, "/api/auth/register", Registration);
        var login = await PostAsync(client, "/api/auth/login", new LoginRequest(Registration.Email, Password));
        var cookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("OnlineStore.Session="));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicCatalogAndHealth_StillAllowGuests()
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();
        foreach (var path in new[] { "/api/health", "/api/catalog", "/api/products", "/api/categories" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task CsrfToken_MustBeRefreshedAfterLogin()
    {
        using var app = new AccountApplication();
        using var client = app.CreateClient();

        // Yeni müşteri oluştur.
        var registration = await PostAsync(
            client, "/api/auth/register", Registration);

        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        // Kullanıcı henüz misafirken token al.
        var tokens = await client.GetFromJsonAsync<JsonElement>(
            "/api/auth/csrf");
        var oldToken = tokens.GetProperty("token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(oldToken));

        // Eski (misafir) token ile giriş yap.
        using var loginRequest = new HttpRequestMessage(
            HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(
                new LoginRequest(Registration.Email, Password))
        };

        loginRequest.Headers.Add("X-CSRF-TOKEN", oldToken);

        using var login = await client.SendAsync(loginRequest);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // Giriş sonrasında eski token ile logout engellenmeli.
        using var oldLogoutRequest = new HttpRequestMessage(
            HttpMethod.Post, "/api/auth/logout")
        {
            Content = JsonContent.Create(new { })
        };

        oldLogoutRequest.Headers.Add("X-CSRF-TOKEN", oldToken);

        using var rejected = await client.SendAsync(oldLogoutRequest);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        // Hatalı logout denemesi oturumu kapatmamalı.
        var stillLoggedIn = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, stillLoggedIn.StatusCode);

        // Giriş yapmış kullanıcı için yeni token al.
        var freshTokens = await client.GetFromJsonAsync<JsonElement>(
            "/api/auth/csrf");
        var freshToken = freshTokens.GetProperty("token").GetString();

        // Yeni token ile çıkış başarılı olmalı.
        using var logoutRequest = new HttpRequestMessage(
            HttpMethod.Post, "/api/auth/logout")
        {
            Content = JsonContent.Create(new { })
        };

        logoutRequest.Headers.Add("X-CSRF-TOKEN", freshToken);

        using var logout = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // Çıkıştan sonra oturum artık aktif olmamalı.
        var afterLogout = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }


    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body)
    {
        var tokens = (await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf"));
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", tokens.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    private sealed class AccountApplication(bool useDatabase = false, bool production = false) : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(production ? "Production" : "Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Server=127.0.0.1;Port=1;Database=account_test;User ID=test;Password=test;Connection Timeout=1;Pooling=false");
            builder.ConfigureServices(services =>
            {
                if (useDatabase) return;
                services.RemoveAll<StoreDbContext>();
                services.AddScoped(_ => new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>()
                    .UseInMemoryDatabase(databaseName).Options));
            });
        }
    }
}
