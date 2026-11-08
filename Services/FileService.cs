using Connectify.API.DTOs;

namespace Connectify.API.Services;

public class FileService : IFileService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileService> _logger;

    private const long MaxFileSizeInBytes = 25 * 1024 * 1024; // 25 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg",
        ".pdf", ".docx", ".doc", ".txt", ".xlsx", ".pptx", ".zip",
        ".mp3", ".wav", ".ogg", ".m4a"
    };

    public FileService(IWebHostEnvironment environment, ILogger<FileService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<FileUploadResultDTO> UploadFileAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("No file payload was uploaded or the file is empty.");
        }

        if (file.Length > MaxFileSizeInBytes)
        {
            throw new ArgumentException($"File size exceeds the permitted limit of {MaxFileSizeInBytes / (1024 * 1024)}MB.");
        }

        var originalExtension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(originalExtension) || !AllowedExtensions.Contains(originalExtension))
        {
            throw new ArgumentException($"File extension '{originalExtension}' is not supported.");
        }

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
        }

        var uploadsDirectory = Path.Combine(webRoot, "uploads");
        if (!Directory.Exists(uploadsDirectory))
        {
            Directory.CreateDirectory(uploadsDirectory);
        }

        var sanitizedExtension = originalExtension.ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid():N}{sanitizedExtension}";
        var destinationPath = Path.Combine(uploadsDirectory, storedFileName);

        await using (var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream);
        }

        _logger.LogInformation("File uploaded successfully: {OriginalName} -> {StoredName} ({Size} bytes)",
            file.FileName, storedFileName, file.Length);

        var attachmentType = DetermineAttachmentType(sanitizedExtension, file.ContentType);
        var relativeUrl = $"/uploads/{storedFileName}";

        return new FileUploadResultDTO
        {
            Url = relativeUrl,
            DownloadUrl = relativeUrl,
            FileName = file.FileName,
            StoredFileName = storedFileName,
            FileSize = file.Length,
            ContentType = file.ContentType,
            AttachmentType = attachmentType
        };
    }

    private static string DetermineAttachmentType(string extension, string contentType)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg")
        {
            return "image";
        }

        if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
            extension is ".mp3" or ".wav" or ".ogg" or ".m4a")
        {
            return "audio";
        }

        if (extension is ".pdf" or ".docx" or ".doc" or ".txt" or ".xlsx" or ".pptx")
        {
            return "document";
        }

        return "file";
    }
}
