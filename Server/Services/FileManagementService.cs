using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Server.Services
{
    // Interface for file management functionality.
    public interface IFileManagementService
    {
        // Uploads a file (given as a Base64 string) and inserts its metadata into the database.
        Task<int> UploadFileAsync(string filename, string base64Content, string author, string metadataJson);

        // Downloads a file by retrieving its path from the database and reading the file from disk.
        Task<string> DownloadFileAsync(int documentId);
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
            // Get the file storage path from configuration or default to "UploadedFiles" directory.
            _fileStoragePath = configuration["FileStoragePath"] ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UploadedFiles");

            // Create the storage directory if it doesn't exist.
            if (!Directory.Exists(_fileStoragePath))
            {
                Directory.CreateDirectory(_fileStoragePath);
                _logger.LogInformation("File storage directory created: {path}", _fileStoragePath);
            }
        }

        public async Task<int> UploadFileAsync(string filename, string base64Content, string author, string metadataJson)
        {
            try
            {
                // Convert the Base64 string to a byte array.
                byte[] fileBytes = Convert.FromBase64String(base64Content);

                // Generate a unique file name to avoid conflicts.
                string uniqueFileName = $"{Guid.NewGuid()}_{filename}";
                string filePath = Path.Combine(_fileStoragePath, uniqueFileName);

                // Save the file to disk.
                await File.WriteAllBytesAsync(filePath, fileBytes);
                _logger.LogInformation("File saved to disk: {filePath}", filePath);

                int documentId;
                // Insert the file metadata into the database.
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"
                        INSERT INTO documents (filename, author, file_path, metadata)
                        VALUES (@filename, @author, @file_path, @metadata)
                        RETURNING id;";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("filename", filename);
                        cmd.Parameters.AddWithValue("author", author);
                        cmd.Parameters.AddWithValue("file_path", filePath);
                        cmd.Parameters.Add(new NpgsqlParameter("metadata", NpgsqlDbType.Jsonb)
                        {
                            Value = metadataJson ?? "{}"
                        });
                        documentId = (int)await cmd.ExecuteScalarAsync();
                    }
                }

                _logger.LogInformation("File metadata inserted into database with document id: {id}", documentId);
                return documentId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file: {filename}", filename);
                throw;
            }
        }

        public async Task<string> DownloadFileAsync(int documentId)
        {
            try
            {
                string filePath = null;
                // Retrieve the file path from the database.
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = "SELECT file_path FROM documents WHERE id = @id;";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("id", documentId);
                        var result = await cmd.ExecuteScalarAsync();
                        if (result == null)
                        {
                            _logger.LogWarning("Document with id {id} not found", documentId);
                            return null;
                        }
                        filePath = result.ToString();
                    }
                }

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found on disk: {filePath}", filePath);
                    return null;
                }

                // Read the file and convert its content to a Base64 string.
                byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                string base64Content = Convert.ToBase64String(fileBytes);
                return base64Content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file with document id: {id}", documentId);
                throw;
            }
        }
    }
}
