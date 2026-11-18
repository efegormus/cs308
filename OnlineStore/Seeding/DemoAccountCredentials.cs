using System.Security.Cryptography;
using System.Text.Json;

namespace OnlineStore.Seeding;

// Private development credentials. Upgrades add accounts without changing existing passwords.
public static class DemoAccountCredentials
{
    public static async Task<Dictionary<string, string>> LoadAsync(string path, IEnumerable<string> emails,
        CancellationToken ct = default)
    {
        var credentials = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(path, ct))
                ?? throw new InvalidDataException("Invalid private demo credential file.")
            : new Dictionary<string, string>();
        var changed = !File.Exists(path);
        foreach (var email in emails)
        {
            if (credentials.TryGetValue(email, out var password))
            {
                if (password is null || password.Length is < 12 or > 128)
                    throw new InvalidDataException("Invalid private demo password. Restore the matching credential backup.");
                continue;
            }
            credentials[email] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "Aa1!";
            changed = true;
        }
        if (changed)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                await using (var stream = new FileStream(temporary, options))
                    await JsonSerializer.SerializeAsync(stream, credentials, new JsonSerializerOptions { WriteIndented = true }, ct);
                File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return credentials;
    }
}
