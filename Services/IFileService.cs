using Connectify.API.DTOs;

namespace Connectify.API.Services;

public interface IFileService
{
    Task<FileUploadResultDTO> UploadFileAsync(IFormFile file);
}
