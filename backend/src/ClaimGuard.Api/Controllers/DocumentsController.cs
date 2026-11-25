using ClaimGuard.Application.DTOs;
using ClaimGuard.Application.Interfaces;
using ClaimGuard.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimGuard.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController(ISupabaseStorageService storage) : ControllerBase
{
    private static readonly string[] AllowedTypes = ["application/pdf", "image/jpeg", "image/png", "image/webp"];
    private const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Uploads a discharge summary / repair estimate into the private Supabase bucket.
    /// The service-role key stays server-side; browsers receive only the stored URL.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxSizeBytes)]
    public async Task<ActionResult<UploadDocumentResponse>> Upload(
        IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) return BadRequest(new { detail = "File is empty." });
        if (file.Length > MaxSizeBytes) return BadRequest(new { detail = "File exceeds the 10 MB limit." });
        if (!AllowedTypes.Contains(file.ContentType))
            return BadRequest(new { detail = "Only PDF, JPEG, PNG or WEBP documents are accepted." });

        await using var stream = file.OpenReadStream();
        var url = await storage.UploadClaimDocumentAsync(
            stream, file.FileName, file.ContentType, User.GetUserId(), ct);

        return Ok(new UploadDocumentResponse(url, url));
    }

    /// <summary>Creates a 1-hour signed URL for a stored document (adjudicator viewing).</summary>
    [HttpGet("signed-url")]
    [Authorize(Roles = "ADJUDICATOR,ADMIN")]
    public async Task<IActionResult> SignedUrl([FromQuery] string path, CancellationToken ct)
    {
        var url = await storage.CreateSignedUrlAsync(path, ct);
        return Ok(new { signedUrl = url });
    }
}
