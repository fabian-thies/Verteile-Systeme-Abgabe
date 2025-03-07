using Shared.Models;

namespace Server.Services;

public interface IFileManagementService
{
    Task<int> UploadFileAsync(string filename, string base64Content, string author, string metadataJson);
    Task<FileDownloadInfo> DownloadFileInfoAsync(int documentId);
}