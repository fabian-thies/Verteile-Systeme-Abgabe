using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Server.Services;

public class AuthService : IAuthService
{
    private readonly string _connectionString;
    private readonly ILogger<AuthService> _logger;

    // English comment: Constructor that retrieves the connection string from configuration and initializes the logger.
    public AuthService(IConfiguration configuration, ILogger<AuthService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _logger = logger;
    }

    // English comment: Checks if the provided credentials are valid.
    public async Task<bool> Login(string username, string password)
    {
        _logger.LogInformation("Login attempt for user: {Username}", username);
        var passwordHash = ComputeSha256Hash(password);

        try
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                _logger.LogDebug("Database connection opened for login");

                var sql = "SELECT COUNT(*) FROM users WHERE username = @username AND password_hash = @password_hash";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("username", username);
                    cmd.Parameters.AddWithValue("password_hash", passwordHash);
                    var userCount = (long)await cmd.ExecuteScalarAsync();
                    var success = userCount > 0;
                    _logger.LogInformation("Login for user {Username} {Result}", username,
                        success ? "successful" : "failed");
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

    // English comment: Registers a new user with the provided credentials.
    public async Task<bool> Register(string username, string password)
    {
        _logger.LogInformation("Registration attempt for user: {Username}", username);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Registration failed: Empty username or password");
            return false;
        }

        var passwordHash = ComputeSha256Hash(password);
        try
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                _logger.LogDebug("Opening database connection for registration");
                await conn.OpenAsync();
                _logger.LogDebug("Database connection opened successfully");

                var sql =
                    "INSERT INTO users (username, password_hash, role) VALUES (@username, @password_hash, 'User')";
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
            _logger.LogWarning("Registration failed: Username {Username} already exists. Error: {Error}", username,
                pgEx.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed for user {Username} with error", username);
            return false;
        }
    }

    // English comment: Computes a SHA256 hash for the provided input string.
    private string ComputeSha256Hash(string rawData)
    {
        using (var sha256Hash = SHA256.Create())
        {
            var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            var builder = new StringBuilder();
            foreach (var b in bytes) builder.Append(b.ToString("x2"));
            return builder.ToString();
        }
    }
}