// Server/Services/FileManagementService.cs

using Npgsql;
using NpgsqlTypes;

namespace Server.Services
{
    public class FileDownloadInfo
    {
        public string FileName { get; set; }
        public string Base64Content { get; set; }
    }

    public interface IFileManagementService
    {
        Task<int> UploadFileAsync(string filename, string base64Content, string author, string metadataJson);
        Task<FileDownloadInfo> DownloadFileInfoAsync(int documentId);
    }

    public class FileManagementService : IFileManagementService
    {
        private readonly string _connectionString;
        private readonly string _fileStoragePath;
        private readonly ILogger<FileManagementService> _logger;

        public FileManagementService(IConfiguration configuration, ILogger<FileManagementService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
            _fileStoragePath = configuration["FileStoragePath"] ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UploadedFiles");
            if (!Directory.Exists(_fileStoragePath))
            {
                Directory.CreateDirectory(_fileStoragePath);
            }
        }

        public async Task<int> UploadFileAsync(string filename, string base64Content, string author, string metadataJson)
        {
            byte[] fileBytes = Convert.FromBase64String(base64Content);
            string uniqueFileName = $"{Guid.NewGuid()}_{filename}";
            string filePath = Path.Combine(_fileStoragePath, uniqueFileName);
            await File.WriteAllBytesAsync(filePath, fileBytes);
            int newVersion = 1;
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string versionSql = "SELECT MAX(version) FROM documents WHERE filename = @filename AND author = @author";
                using (var versionCmd = new NpgsqlCommand(versionSql, conn))
                {
                    versionCmd.Parameters.AddWithValue("filename", filename);
                    versionCmd.Parameters.AddWithValue("author", author);
                    var result = await versionCmd.ExecuteScalarAsync();
                    if (result != DBNull.Value && result != null)
                    {
                        newVersion = Convert.ToInt32(result) + 1;
                    }
                }
                string sql = "INSERT INTO documents (filename, author, file_path, metadata, version, last_modified) VALUES (@filename, @author, @file_path, @metadata, @version, NOW()) RETURNING id;";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("filename", filename);
                    cmd.Parameters.AddWithValue("author", author);
                    cmd.Parameters.AddWithValue("file_path", filePath);
                    cmd.Parameters.Add(new NpgsqlParameter("metadata", NpgsqlDbType.Jsonb)
                    {
                        Value = string.IsNullOrEmpty(metadataJson) ? "{}" : metadataJson
                    });
                    cmd.Parameters.AddWithValue("version", newVersion);
                    int documentId = (int)await cmd.ExecuteScalarAsync();
                    return documentId;
                }
            }
        }

        public async Task<FileDownloadInfo> DownloadFileInfoAsync(int documentId)
        {
            string filePath = null;
            string originalFileName = null;
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string sql = "SELECT file_path, filename FROM documents WHERE id = @id;";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", documentId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            filePath = reader.GetString(0);
                            originalFileName = reader.GetString(1);
                        }
                    }
                }
            }
            if (filePath == null || !File.Exists(filePath))
            {
                return null;
            }
            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
            string base64Content = Convert.ToBase64String(fileBytes);
            return new FileDownloadInfo { FileName = originalFileName, Base64Content = base64Content };
        }
    }
}
