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

    /// <summary>
    /// For attaching an image already sitting in the bucket (e.g. uploaded
    /// directly via the Cloudflare dashboard) instead of through UploadAsync.
    /// Only accepts URLs under this bucket's own PublicBaseUrl — a link to
    /// someone else's image would leave GetPublicUrl/DeleteAsync operating on
    /// a key that isn't actually in our bucket.
    /// </summary>
    bool TryGetKeyFromUrl(string url, out string key);
}
