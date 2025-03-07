using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using Server.Services;

namespace Server.Hubs
{
    public class DocumentVersion
    {
        public int Id { get; set; }
        public string Filename { get; set; }
        public string Author { get; set; }
        public int Version { get; set; }
        public DateTime UploadTimestamp { get; set; }
    }

    public class ChatHub : Hub
    {
        private static ConcurrentDictionary<string, string> _userConnections = new ConcurrentDictionary<string, string>();
        private readonly IAuthService _authService;
        private readonly ILogger<ChatHub> _logger;
        private readonly IFileManagementService _fileService;
        private readonly string _connectionString;

        public ChatHub(IAuthService authService, ILogger<ChatHub> logger, IFileManagementService fileService, IConfiguration config)
        {
            _authService = authService;
            _logger = logger;
            _fileService = fileService;
            _connectionString = config.GetConnectionString("DefaultConnection");
        }

        public async Task<bool> Login(string username, string password)
        {
            bool isAuthenticated = await _authService.Login(username, password);
            if (isAuthenticated)
            {
                _userConnections[Context.ConnectionId] = username;
                await Groups.AddToGroupAsync(Context.ConnectionId, username);
                await Clients.All.SendAsync("ReceiveSystemMessage", username + " has logged in.");
            }
            return isAuthenticated;
        }

        public async Task<bool> Register(string username, string password)
        {
            return await _authService.Register(username, password);
        }

        public async Task SendPrivateMessage(string targetUser, string message)
        {
            if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
            {
                await Clients.Group(targetUser).SendAsync("ReceivePrivateMessage", sender, message);
            }
        }

        public async Task JoinGroup(string groupName)
        {
            var username = _userConnections.GetValueOrDefault(Context.ConnectionId);
            if (string.IsNullOrEmpty(username))
            {
                return;
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", "[System]", username + " has joined the group " + groupName + ".");
        }

        public async Task LeaveGroup(string groupName)
        {
            var username = _userConnections.GetValueOrDefault(Context.ConnectionId);
            if (string.IsNullOrEmpty(username))
            {
                return;
            }
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", "[System]", username + " has left the group " + groupName + ".");
        }

        public async Task SendGroupMessage(string groupName, string message)
        {
            if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
            {
                await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", sender, message);
            }
        }

        public async Task SendWhiteboardLine(string target, bool isGroup, double x1, double y1, double x2, double y2)
        {
            await Clients.Group(target).SendAsync("ReceiveWhiteboardLine", x1, y1, x2, y2);
        }

        public async Task SendWhiteboardLineBroadcast(double x1, double y1, double x2, double y2)
        {
            await Clients.All.SendAsync("ReceiveWhiteboardLine", x1, y1, x2, y2);
        }

        public async Task RequestWhiteboardPlugin(string targetUser)
        {
            if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
            {
                await Clients.Group(targetUser).SendAsync("ReceiveWhiteboardPluginRequest", sender);
            }
        }

        public async Task SendPluginFile(string targetUser, string base64PluginContent)
        {
            if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
            {
                await Clients.Group(targetUser).SendAsync("ReceivePluginFile", sender, base64PluginContent);
            }
        }

        public async Task RequestPluginFile(string requester)
        {
            if (_userConnections.TryGetValue(Context.ConnectionId, out var requestingUser))
            {
                await Clients.Group(requester).SendAsync("ReceivePluginFileRequest", requestingUser);
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (_userConnections.TryRemove(Context.ConnectionId, out var username))
            {
                await Clients.All.SendAsync("ReceiveSystemMessage", username + " has logged out.");
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task<int> UploadDocument(string filename, string base64Content, string author, string metadata)
        {
            if (!_userConnections.TryGetValue(Context.ConnectionId, out var a))
            {
                return -1;
            }
            int documentId = await _fileService.UploadFileAsync(filename, base64Content, a, metadata);
            await Clients.All.SendAsync("ReceiveSystemMessage", a + " uploaded document " + filename + ".");
            return documentId;
        }

        public async Task<string> DownloadDocument(int documentId)
        {
            var fileInfo = await _fileService.DownloadFileInfoAsync(documentId);
            if (fileInfo == null) return null;
            return System.Text.Json.JsonSerializer.Serialize(fileInfo);
        }

        public async Task<List<DocumentVersion>> GetDocumentVersionsById(int fileId)
        {
            var list = new List<DocumentVersion>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = "SELECT id, filename, author, version, upload_timestamp FROM documents WHERE id = @id ORDER BY version DESC";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", fileId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DocumentVersion
                {
                    Id = reader.GetInt32(0),
                    Filename = reader.GetString(1),
                    Author = reader.GetString(2),
                    Version = reader.GetInt32(3),
                    UploadTimestamp = reader.GetDateTime(4)
                });
            }
            return list;
        }

        public async Task<List<DocumentVersion>> GetAllDocuments()
        {
            var list = new List<DocumentVersion>();
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = "SELECT id, filename, author, version, upload_timestamp FROM documents ORDER BY id ASC";
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new DocumentVersion
                {
                    Id = reader.GetInt32(0),
                    Filename = reader.GetString(1),
                    Author = reader.GetString(2),
                    Version = reader.GetInt32(3),
                    UploadTimestamp = reader.GetDateTime(4)
                });
            }
            return list;
        }
    }
}
