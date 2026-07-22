using Homeji.Application.DTOs.Upload;
using Homeji.Application.IServices.Upload;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/upload")]
public sealed class UploadController : ControllerBase
{
    private readonly IImageUploadService _imageUploadService;

    public UploadController(IImageUploadService imageUploadService)
    {
        _imageUploadService = imageUploadService;
    }

    /// <summary>
    /// Upload one or more images to Cloudinary. Always returns an array of URLs.
    /// Used by landlords (rental post images) and users (avatar, reports, etc.).
    /// </summary>
    [HttpPost("image")]
    [ProducesResponseType<IReadOnlyList<UploadImageResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB total
    public async Task<ActionResult<IReadOnlyList<UploadImageResultDto>>> UploadImages(
        IReadOnlyList<IFormFile> files,
        [FromQuery] string? folder,
        CancellationToken cancellationToken)
    {
        if (files is null || files.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Chưa chọn tệp",
                Detail = "Vui lòng chọn ít nhất một ảnh để tải lên.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        if (files.Count > 10)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Quá nhiều tệp",
                Detail = "Mỗi lần chỉ được tải lên tối đa 10 ảnh.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var results = new List<UploadImageResultDto>(files.Count);

        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();
            var result = await _imageUploadService.UploadAsync(
                file.FileName,
                file.ContentType,
                stream,
                folder,
                cancellationToken);

            results.Add(result);
        }

        return Ok(results);
    }
}
