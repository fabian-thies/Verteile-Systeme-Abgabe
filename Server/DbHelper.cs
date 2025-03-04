using Npgsql;

namespace Server.Database
{
    public static class DbHelper
    {
        // Connection string to PostgreSQL
        // Replace with your actual credentials and DB details
        private static readonly string _connectionString = "Host=91.218.66.12;Port=4462;Database=postgres;Username=postgres;Password=FeK2olA9hIFHQiaeip3c2uHWm1YTNerTYCkKM3sptkFayUNxN7nvVqK7d2rqmTdn";

        /// <summary>
        /// Gets a new connection to the PostgreSQL database.
        /// </summary>
        public static NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }
    }
}