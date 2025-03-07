using Npgsql;
using NpgsqlTypes;
using Shared.Models;

namespace Server.Services;

public class FileManagementService : IFileManagementService
{
    private readonly string _connectionString;
    private readonly string _fileStoragePath;
    private readonly ILogger<FileManagementService> _logger;

    public FileManagementService(IConfiguration configuration, ILogger<FileManagementService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _logger = logger;
        _fileStoragePath = configuration["FileStoragePath"] ??
                           Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UploadedFiles");

        // Create the file storage directory if it does not exist
        if (!Directory.Exists(_fileStoragePath))
        {
            Directory.CreateDirectory(_fileStoragePath);
            _logger.LogInformation("Created file storage directory at {FileStoragePath}", _fileStoragePath);
        }
        else
        {
            _logger.LogInformation("Using existing file storage directory at {FileStoragePath}", _fileStoragePath);
        }
    }

    public async Task<int> UploadFileAsync(string filename, string base64Content, string author, string metadataJson)
    {
        _logger.LogInformation("Starting file upload for file: {Filename} by author: {Author}", filename, author);

        // Convert base64 content to bytes
        var fileBytes = Convert.FromBase64String(base64Content);
        var uniqueFileName = $"{Guid.NewGuid()}_{filename}";
        var filePath = Path.Combine(_fileStoragePath, uniqueFileName);

        // Write file to disk
        await File.WriteAllBytesAsync(filePath, fileBytes);
        _logger.LogInformation("File written to disk at {FilePath}", filePath);

        // Determine new version number
        var newVersion = 1;
        using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            _logger.LogInformation("Database connection opened for version check.");

            var versionSql = "SELECT MAX(version) FROM documents WHERE filename = @filename AND author = @author";
            using (var versionCmd = new NpgsqlCommand(versionSql, conn))
            {
                versionCmd.Parameters.AddWithValue("filename", filename);
                versionCmd.Parameters.AddWithValue("author", author);
                var result = await versionCmd.ExecuteScalarAsync();
                if (result != DBNull.Value && result != null)
                {
                    newVersion = Convert.ToInt32(result) + 1;
                    _logger.LogInformation("Existing version found. Incremented version to {Version}", newVersion);
                }
                else
                {
                    _logger.LogInformation("No existing version found. Starting with version {Version}", newVersion);
                }
            }

            // Insert document record into database
            var sql =
                "INSERT INTO documents (filename, author, file_path, metadata, version, last_modified) VALUES (@filename, @author, @file_path, @metadata, @version, NOW()) RETURNING id;";
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

                _logger.LogInformation("Executing database insert for file upload.");
                var documentId = (int)await cmd.ExecuteScalarAsync();
                _logger.LogInformation("File uploaded successfully with document ID: {DocumentId}", documentId);
                return documentId;
            }
        }
    }

    public async Task<FileDownloadInfo> DownloadFileInfoAsync(int documentId)
    {
        _logger.LogInformation("Starting file download for document ID: {DocumentId}", documentId);
        string filePath = null;
        string originalFileName = null;
        using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            _logger.LogInformation("Database connection opened for file download.");

            var sql = "SELECT file_path, filename FROM documents WHERE id = @id;";
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", documentId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        filePath = reader.GetString(0);
                        originalFileName = reader.GetString(1);
                        _logger.LogInformation("File record found for document ID: {DocumentId}", documentId);
                    }
                    else
                    {
                        _logger.LogWarning("No file record found for document ID: {DocumentId}", documentId);
                    }
                }
            }
        }

        // Check if file exists
        if (filePath == null || !File.Exists(filePath))
        {
            _logger.LogError("File not found at path: {FilePath} for document ID: {DocumentId}", filePath, documentId);
            return null;
        }

        // Read file from disk and convert to base64
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var base64Content = Convert.ToBase64String(fileBytes);
        _logger.LogInformation("File read successfully from disk for document ID: {DocumentId}", documentId);
        return new FileDownloadInfo { FileName = originalFileName, Base64Content = base64Content };
    }
}