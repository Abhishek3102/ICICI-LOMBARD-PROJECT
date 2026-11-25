using ClaimGuard.Application.Interfaces;
using ClaimGuard.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace ClaimGuard.Infrastructure.Storage;

public interface ISupabaseStorageService
{
    /// <summary>Uploads into the private `claim-documents` bucket using the service-role key.</summary>
    Task<string> UploadClaimDocumentAsync(Stream content, string fileName, string contentType, Guid userId, CancellationToken ct = default);

    /// <summary>Creates a 1-hour signed URL so adjudicators can view a private document.</summary>
    Task<string> CreateSignedUrlAsync(string objectPath, CancellationToken ct = default);
}

/// <summary>
/// Talks to Supabase Storage over its REST API (no SDK dependency). The service-role key
/// never leaves the server; browsers only ever receive short-lived signed URLs.
/// </summary>
public class SupabaseStorageService : ISupabaseStorageService
{
    private readonly HttpClient _http;
    private readonly SupabaseOptions _options;

    public SupabaseStorageService(HttpClient http, IOptions<SupabaseOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> UploadClaimDocumentAsync(Stream content, string fileName, string contentType, Guid userId, CancellationToken ct = default)
    {
        var objectPath = $"{userId:N}/{DateTime.UtcNow:yyyyMMdd}/{fileName}";
        var url = $"{_options.Url}/storage/v1/object/{_options.DocumentsBucket}/{objectPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StreamContent(content) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);
        request.Headers.Add("x-upsert", "true");

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return $"{_options.Url}/storage/v1/object/{_options.DocumentsBucket}/{objectPath}";
    }

    public async Task<string> CreateSignedUrlAsync(string objectPath, CancellationToken ct = default)
    {
        var url = $"{_options.Url}/storage/v1/object/sign/{_options.DocumentsBucket}/{objectPath}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new { expiresIn = 3600 })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var signed = doc.RootElement.GetProperty("signedURL").GetString();
        return $"{_options.Url}{signed}";
    }
}
