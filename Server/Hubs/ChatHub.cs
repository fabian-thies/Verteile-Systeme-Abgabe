using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace Server.Hubs
{
    public class ChatHub : Hub
    {
        private readonly string _connectionString;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(IConfiguration configuration, ILogger<ChatHub> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
            _logger.LogInformation("ChatHub initialized with connection string: {ConnectionString}", 
                _connectionString?.Substring(0, Math.Min(_connectionString?.Length ?? 0, 20)) + "...");
        }

        public async Task<bool> Login(string username, string password)
        {
            _logger.LogInformation("Login attempt for user: {Username}", username);
            string passwordHash = ComputeSha256Hash(password);
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    _logger.LogDebug("Database connection opened for login");
                    
                    string sql = "SELECT COUNT(*) FROM users WHERE username = @username AND password_hash = @password_hash";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("username", username);
                        cmd.Parameters.AddWithValue("password_hash", passwordHash);
                        long userCount = (long)await cmd.ExecuteScalarAsync();
                        bool success = userCount > 0;
                        _logger.LogInformation("Login for user {Username} {Result}", username, success ? "successful" : "failed");
                        return success;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {Username}", username);
                return false;
            }
        }

        public async Task<bool> Register(string username, string password)
        {
            _logger.LogInformation("Registration attempt for user: {Username}", username);
            
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Registration failed: Empty username or password");
                return false;
            }
            
            string passwordHash = ComputeSha256Hash(password);
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    _logger.LogDebug("Opening database connection for registration");
                    await conn.OpenAsync();
                    _logger.LogDebug("Database connection opened successfully");
                    
                    string sql = "INSERT INTO users (username, password_hash, role) VALUES (@username, @password_hash, 'User')";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("username", username);
                        cmd.Parameters.AddWithValue("password_hash", passwordHash);
                        _logger.LogDebug("Executing registration SQL command");
                        await cmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("User {Username} registered successfully", username);
                        return true;
                    }
                }
            }
            catch (PostgresException pgEx) when (pgEx.SqlState == "23505")
            {
                _logger.LogWarning("Registration failed: Username {Username} already exists. Error: {Error}", 
                    username, pgEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed for user {Username} with error", username);
                return false;
            }
        }

        private string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}