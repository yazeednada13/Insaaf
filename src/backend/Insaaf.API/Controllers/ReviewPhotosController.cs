using Insaaf.Application.Photos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Insaaf.API.Controllers;

/// <summary>
/// Serves review photo images (Phase 9A, D-6). Anonymous, because React Native's Image doesn't go through
/// the Axios refresh interceptor; ids are random GUIDs, and only Ready, attached photos of public reviews
/// in active restaurants are served. Everything else is 404, so moderation applies on every request.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/review-photos")]
public sealed class ReviewPhotosController : ControllerBase
{
    private readonly IReviewPhotoService _photos;

    public ReviewPhotosController(IReviewPhotoService photos)
    {
        _photos = photos;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var photo = await _photos.OpenServableAsync(id, cancellationToken);
        if (photo is null)
        {
            return NotFound();
        }

        // Shared caches must not store it; clients revalidate before every reuse (D-11).
        Response.Headers[HeaderNames.CacheControl] = "private, no-cache";
        Response.Headers[HeaderNames.ETag] = photo.ETag;
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        if (Request.Headers.IfNoneMatch.Any(value => value == photo.ETag))
        {
            await photo.Content.DisposeAsync();
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return File(photo.Content, "image/jpeg");
    }
}
