using OnlineStore.Purchasing;

namespace OnlineStore.Tests;

public class MockPaymentTests
{
    [Theory]
    [InlineData("4242424242424242", 12, 2030, "123", true)]
    [InlineData("4000000000000002", 12, 2030, "123", false)]
    public async Task MockPayment_UsesDeterministicApprovalWithoutPersistingCard(string number, int month, int year, string code, bool approved)
    {
        var gateway = new MockPaymentGateway(new FixedClock());
        var card = new MockCard { Number = number, ExpiryMonth = month, ExpiryYear = year, SecurityCode = code };
        var first = await gateway.AuthorizeAsync(card, 100, "opaque-key", default);
        Assert.Equal(approved, first.Approved);
        Assert.Equal(first, await gateway.AuthorizeAsync(card, 100, "opaque-key", default));
        Assert.DoesNotContain(number, first.ToString());
        Assert.DoesNotContain(number, card.ToString());
    }
    [Theory]
    [InlineData("4242424242424241", 12, 2030, "123")]
    [InlineData("4242424242424242", 0, 2030, "123")]
    [InlineData("4242424242424242", 1, 2026, "123")]
    [InlineData("4242424242424242", 12, 2030, "ab3")]
    [InlineData("4242424242424242", 12, 2030, "12345")]
    [InlineData("424242424242424242424242", 12, 2030, "123")]
    public async Task MockPayment_RejectsInvalidBoundedInput(string number, int month, int year, string code)
    {
        var gateway = new MockPaymentGateway(new FixedClock());
        var error = await Assert.ThrowsAsync<PurchaseException>(() => gateway.AuthorizeAsync(new MockCard
            { Number = number, ExpiryMonth = month, ExpiryYear = year, SecurityCode = code }, 100, "key", default));
        Assert.Equal(400, error.Status);
        Assert.DoesNotContain(number, error.Message);
    }
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    }
}
