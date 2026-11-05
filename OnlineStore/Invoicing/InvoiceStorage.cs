using System.Text.RegularExpressions;

namespace OnlineStore.Invoicing;

// Where generated PDFs are kept. Keys are file names chosen by InvoiceService, never user input.
public interface IInvoiceStorage
{
    Task SaveAsync(string key, byte[] content, CancellationToken ct);
    Task<byte[]?> ReadAsync(string key, CancellationToken ct);
}

// Stores PDFs on disk outside wwwroot, so the static file middleware can never serve them.
public sealed partial class FileInvoiceStorage : IInvoiceStorage
{
    private readonly string root;

    public FileInvoiceStorage(string root)
    {
        this.root = Path.GetFullPath(root);
        Directory.CreateDirectory(this.root);
    }

    public async Task SaveAsync(string key, byte[] content, CancellationToken ct)
    {
        var path = PathFor(key);
        // Write to a temporary file first, so a reader never sees a half-written PDF.
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, content, ct);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public async Task<byte[]?> ReadAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }

    private string PathFor(string key)
    {
        if (!KeyPattern().IsMatch(key)) throw new ArgumentException("Invalid invoice document key.", nameof(key));
        return Path.Combine(root, key);
    }

    [GeneratedRegex("^[A-Z0-9-]{1,64}\\.pdf$")]
    private static partial Regex KeyPattern();
}
