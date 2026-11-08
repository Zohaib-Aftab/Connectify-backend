namespace Connectify.API.DTOs;

public class FileUploadResultDTO
{
    public string Url { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string AttachmentType { get; set; } = "file"; // "image", "document", "audio", "file"
}
