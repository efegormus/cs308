using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace OnlineStore.Tests;

// CI/model-only tests have no developer secrets. Supply an isolated test key file.
internal static class TestDataKeys
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        if (Environment.GetEnvironmentVariable("ONLINESTORE_DATA_KEYS_FILE") is not null) return;
        var path = Path.Combine(Path.GetTempPath(), $"onlinestore-test-keys-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            version = 1,
            encryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            blindIndexKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Environment.SetEnvironmentVariable("ONLINESTORE_DATA_KEYS_FILE", path);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => File.Delete(path);
    }
}
