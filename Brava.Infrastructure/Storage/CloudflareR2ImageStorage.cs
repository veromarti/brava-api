using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Brava.Application;
using Microsoft.Extensions.Configuration;

namespace Brava.Infrastructure.Storage;

/// <summary>
/// R2 speaks the S3 API, so this is a plain AmazonS3Client pointed at R2's
/// endpoint rather than a Cloudflare-specific SDK.
///
/// Config is read lazily (on first upload/delete/URL build), not in the
/// constructor — Vero doesn't have an R2 bucket yet, and eagerly validating
/// this at DI-registration time would crash the whole API on startup for
/// every endpoint, not just the image ones, until R2 is set up. Once a
/// ProductImage row exists, a successful upload already proved the config
/// works, so GetPublicUrl never hits the missing-config case in practice.
/// </summary>
public class CloudflareR2ImageStorage(IConfiguration configuration) : IImageStorage
{
    private AmazonS3Client? _client;
    private string? _bucketName;
    private string? _publicBaseUrl;

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        EnsureConfigured();
        await _client!.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            // R2 doesn't implement the SDK's default aws-chunked streaming
            // upload (SigV4 chunked signing) — request bodies must go over in
            // one signed payload instead. HTTPS still covers transport
            // integrity for this MVP's image sizes.
            UseChunkEncoding = false,
        }, ct);
        return key;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        EnsureConfigured();
        await _client!.DeleteObjectAsync(_bucketName, key, ct);
    }

    public string GetPublicUrl(string key)
    {
        EnsureConfigured();
        return $"{_publicBaseUrl!.TrimEnd('/')}/{key}";
    }

    private void EnsureConfigured()
    {
        if (_client is not null)
        {
            return;
        }

        var accountId = Require("R2:AccountId");
        var accessKeyId = Require("R2:AccessKeyId");
        var secretAccessKey = Require("R2:SecretAccessKey");
        _bucketName = Require("R2:BucketName");
        _publicBaseUrl = Require("R2:PublicBaseUrl");

        _client = new AmazonS3Client(
            new BasicAWSCredentials(accessKeyId, secretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                // The SDK defaults to WHEN_SUPPORTED, which streams a trailing
                // checksum (STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER) that R2
                // doesn't implement and rejects outright. R2 doesn't require
                // this, so WHEN_REQUIRED avoids it.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            });
    }

    private string Require(string key) =>
        configuration[key] ?? throw new InvalidOperationException(
            $"Configuration '{key}' is not set. Image storage (R2) is not configured yet — " +
            "see CLAUDE.md's stack table and the conversation this was scaffolded in for what's needed.");
}
