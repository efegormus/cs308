using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Accounts;

public static class RoleCommand
{
    public const string Flag = "--set-role";

    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        var index = Array.IndexOf(args, Flag);
        if (index < 0 || index + 2 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1])
            || !Enum.GetNames<UserRole>().Contains(args[index + 2], StringComparer.Ordinal))
        {
            Console.Error.WriteLine("Usage: --set-role <existing-account-email> <Customer|SalesManager|ProductManager|SupportAgent>");
            return 1;
        }
        try
        {
            using var scope = app.Services.CreateScope();
            var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
            var found = await accounts.SetRoleAsync(args[index + 1], Enum.Parse<UserRole>(args[index + 2]));
            if (!found) { Console.Error.WriteLine("Account not found. Create the account before assigning its role."); return 1; }
            Console.WriteLine("Role updated. Existing sessions for this account have been revoked.");
            return 0;
        }
        catch (Exception exception) when (DatabaseErrors.IsUnavailable(exception))
        {
            Console.Error.WriteLine("Could not update the account. Check the database configuration and migrations.");
            return 1;
        }
    }
}
