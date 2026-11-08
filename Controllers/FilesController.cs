using Connectify.API.DTOs;
using Connectify.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connectify.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(IFileService fileService, ILogger<FilesController> logger)
    {
        _fileService = fileService;
        _logger = logger;
    }

    /// <summary>
    /// Uploads an attachment file (image, document, or audio clip) to secure static storage.
    /// </summary>
    /// <param name="file">Multi-part form file stream.</param>
    /// <returns>FileUploadResultDTO containing relative static download URL and file metadata.</returns>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FileUploadResultDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Please select a valid non-empty file to upload." });
        }

        try
        {
            var result = await _fileService.UploadFileAsync(file);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file upload for {FileName}", file.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while uploading the file." });
        }
    }
}
