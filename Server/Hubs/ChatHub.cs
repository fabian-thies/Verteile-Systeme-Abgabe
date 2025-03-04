using Microsoft.AspNetCore.SignalR;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace Server.Hubs
{
    public class ChatHub : Hub
    {
        private readonly string _connectionString;

        public ChatHub(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<bool> Login(string username, string password)
        {
            string passwordHash = ComputeSha256Hash(password);
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql =
                        "SELECT COUNT(*) FROM users WHERE username = @username AND password_hash = @password_hash";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("username", username);
                        cmd.Parameters.AddWithValue("password_hash", passwordHash);
                        long userCount = (long)await cmd.ExecuteScalarAsync();
                        return userCount > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> Register(string username, string password)
        {
            string passwordHash = ComputeSha256Hash(password);
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql =
                        "INSERT INTO users (username, password_hash, role) VALUES (@username, @password_hash, 'User')";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("username", username);
                        cmd.Parameters.AddWithValue("password_hash", passwordHash);
                        await cmd.ExecuteNonQueryAsync();
                        return true;
                    }
                }
            }
            catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "23505")
            {
                return false;
            }
            catch
            {
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