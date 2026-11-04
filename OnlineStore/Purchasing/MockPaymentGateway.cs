namespace OnlineStore.Purchasing;

public sealed record MockPaymentResult(bool Approved, string? Reference);

// SCRUM-73 can replace this registration without changing the checkout transaction.
// This gateway is deliberately local and deterministic: no money is moved.
public interface ICheckoutPaymentGateway
{
    Task<MockPaymentResult> AuthorizeAsync(MockCard card, decimal amount, string attemptKey, CancellationToken ct);
}

public sealed class MockPaymentGateway(TimeProvider clock) : ICheckoutPaymentGateway
{
    public Task<MockPaymentResult> AuthorizeAsync(MockCard card, decimal amount, string attemptKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var now = clock.GetUtcNow();
        var number = card.Number;
        var code = card.SecurityCode;
        if (number is null || number.Length is < 13 or > 19 || number.Any(c => c is < '0' or > '9')
            || !PassesChecksum(number) || code is null || code.Length is < 3 or > 4
            || code.Any(c => c is < '0' or > '9') || card.ExpiryMonth is < 1 or > 12
            || card.ExpiryYear < now.Year || card.ExpiryYear > now.Year + 20
            || (card.ExpiryYear == now.Year && card.ExpiryMonth < now.Month) || amount < 0)
            throw new PurchaseException(400, "Enter valid mock payment details.");

        // The documented decline card lets the UI and tests exercise failure safely.
        var approved = number != "4000000000000002";
        return Task.FromResult(new MockPaymentResult(approved, approved ? "mock_" + attemptKey : null));
    }

    private static bool PassesChecksum(string number)
    {
        var sum = 0;
        var twice = false;
        for (var index = number.Length - 1; index >= 0; index--)
        {
            var digit = number[index] - '0';
            if (twice) { digit *= 2; if (digit > 9) digit -= 9; }
            sum += digit;
            twice = !twice;
        }
        return sum % 10 == 0;
    }
}
