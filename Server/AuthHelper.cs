// AuthHelper.cs
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

namespace Server.Database
{
    public static class AuthHelper
    {
        /// <summary>
        /// Generates a SHA256 hash for the given input.
        /// </summary>
        public static string ComputeHash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// Registers a new user with username and password.
        /// </summary>
        public static async Task<bool> RegisterUserAsync(string username, string password)
        {
            string passwordHash = ComputeHash(password);
            using (var conn = DbHelper.GetConnection())
            {
                await conn.OpenAsync();
                // Check if username exists
                using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE username = @username", conn))
                {
                    checkCmd.Parameters.AddWithValue("username", username);
                    long count = (long)await checkCmd.ExecuteScalarAsync();
                    if (count > 0)
                    {
                        return false; // User already exists
                    }
                }
                // Insert new user
                using (var cmd = new NpgsqlCommand("INSERT INTO users (username, password_hash, role) VALUES (@username, @passwordHash, 'user')", conn))
                {
                    cmd.Parameters.AddWithValue("username", username);
                    cmd.Parameters.AddWithValue("passwordHash", passwordHash);
                    int result = await cmd.ExecuteNonQueryAsync();
                    return result > 0;
                }
            }
        }

        /// <summary>
        /// Logs in a user by verifying username and password.
        /// </summary>
        public static async Task<bool> LoginUserAsync(string username, string password)
        {
            string passwordHash = ComputeHash(password);
            using (var conn = DbHelper.GetConnection())
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand("SELECT password_hash FROM users WHERE username = @username", conn))
                {
                    cmd.Parameters.AddWithValue("username", username);
                    var dbHash = await cmd.ExecuteScalarAsync() as string;
                    return dbHash != null && dbHash == passwordHash;
                }
            }
        }
    }
}
