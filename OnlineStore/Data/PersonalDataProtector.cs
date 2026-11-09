using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OnlineStore.Data;

// Wire format v1: 12-byte random nonce | ciphertext | 16-byte authentication tag.
// Keys are outside MySQL and source control. Email lookup uses a separate HMAC key.
public sealed class PersonalDataProtector(byte[] encryptionKey, byte[] blindIndexKey)
{
    private static readonly PersonalDataProtector TestKeys = new(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));
    public static PersonalDataProtector Load(bool inMemory = false)
    {
        if (inMemory) return TestKeys;
        var configured = Environment.GetEnvironmentVariable("ONLINESTORE_DATA_KEYS_FILE");
        var path = configured;
        if (path is null)
        {
            for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, ".local", "db-redesign", "data-keys.json");
                if (File.Exists(candidate)) { path = candidate; break; }
            }
        }
        if (path is null || !File.Exists(path))
            throw new InvalidOperationException("Set ONLINESTORE_DATA_KEYS_FILE to the private database encryption key file. See docs/db-redesign/local-setup.md.");
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        if (json.RootElement.GetProperty("version").GetInt32() != 1) throw new InvalidOperationException("Unsupported data key version.");
        var encryption = Convert.FromBase64String(json.RootElement.GetProperty("encryptionKey").GetString()!);
        var index = Convert.FromBase64String(json.RootElement.GetProperty("blindIndexKey").GetString()!);
        if (encryption.Length != 32 || index.Length != 32) throw new InvalidOperationException("Data keys must be 32 bytes.");
        return new(encryption, index);
    }

    public byte[] Encrypt(string value)
    {
        var plain = Encoding.UTF8.GetBytes(value);
        var result = new byte[12 + plain.Length + 16];
        RandomNumberGenerator.Fill(result.AsSpan(0, 12));
        using var aes = new AesGcm(encryptionKey, 16);
        aes.Encrypt(result.AsSpan(0, 12), plain, result.AsSpan(12, plain.Length), result.AsSpan(12 + plain.Length, 16));
        return result;
    }

    public string Decrypt(byte[] value)
    {
        if (value.Length < 28) throw new CryptographicException("Invalid encrypted personal data.");
        var plain = new byte[value.Length - 28];
        using var aes = new AesGcm(encryptionKey, 16);
        aes.Decrypt(value.AsSpan(0, 12), value.AsSpan(12, plain.Length), value.AsSpan(12 + plain.Length, 16), plain);
        return Encoding.UTF8.GetString(plain);
    }

    public byte[] EmailIndex(string email) => HMACSHA256.HashData(blindIndexKey,
        Encoding.UTF8.GetBytes(email.Trim().ToUpperInvariant()));
}
