namespace Brava.Application;

/// <summary>
/// The seam for product image storage — mirrors IBravaDbContext's role for
/// persistence. Application depends on this interface; Infrastructure
/// implements it against Cloudflare R2 (see CLAUDE.md's stack table).
/// </summary>
public interface IImageStorage
{
    Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Pure string formatting, no network call — safe to call for any key.</summary>
    string GetPublicUrl(string key);
}
