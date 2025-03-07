using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using Server.Services;
using Shared.Models;

namespace Server.Hubs;

public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> _userConnections = new();

    private readonly IAuthService _authService;
    private readonly string _connectionString;
    private readonly IFileManagementService _fileService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IAuthService authService, ILogger<ChatHub> logger, IFileManagementService fileService,
        IConfiguration config)
    {
        _authService = authService;
        _logger = logger;
        _fileService = fileService;
        _connectionString = config.GetConnectionString("DefaultConnection");
    }

    public async Task<bool> Login(string username, string password)
    {
        var isAuthenticated = await _authService.Login(username, password);
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
            await Clients.Group(targetUser).SendAsync("ReceivePrivateMessage", sender, message);
    }

    public async Task JoinGroup(string groupName)
    {
        var username = _userConnections.GetValueOrDefault(Context.ConnectionId);
        if (string.IsNullOrEmpty(username)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", "[System]",
            username + " has joined the group " + groupName + ".");
    }

    public async Task LeaveGroup(string groupName)
    {
        var username = _userConnections.GetValueOrDefault(Context.ConnectionId);
        if (string.IsNullOrEmpty(username)) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", "[System]",
            username + " has left the group " + groupName + ".");
    }

    public async Task SendGroupMessage(string groupName, string message)
    {
        if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", sender, message);
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
            await Clients.Group(targetUser).SendAsync("ReceiveWhiteboardPluginRequest", sender);
    }

    public async Task SendPluginFile(string targetUser, string base64PluginContent)
    {
        if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
            await Clients.Group(targetUser).SendAsync("ReceivePluginFile", sender, base64PluginContent);
    }

    public async Task RequestPluginFile(string requester)
    {
        if (_userConnections.TryGetValue(Context.ConnectionId, out var requestingUser))
            await Clients.Group(requester).SendAsync("ReceivePluginFileRequest", requestingUser);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        if (_userConnections.TryRemove(Context.ConnectionId, out var username))
            await Clients.All.SendAsync("ReceiveSystemMessage", username + " has logged out.");

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<int> UploadDocument(string filename, string base64Content, string author, string metadata)
    {
        if (!_userConnections.TryGetValue(Context.ConnectionId, out var a)) return -1;

        var documentId = await _fileService.UploadFileAsync(filename, base64Content, a, metadata);
        await Clients.All.SendAsync("ReceiveSystemMessage", a + " uploaded document " + filename + ".");
        return documentId;
    }

    public async Task<string> DownloadDocument(int documentId)
    {
        var fileInfo = await _fileService.DownloadFileInfoAsync(documentId);
        if (fileInfo == null) return null;
        return JsonSerializer.Serialize(fileInfo);
    }

    public async Task<List<DocumentVersion>> GetDocumentVersionsById(int fileId)
    {
        DocumentVersion baseDoc = null;
        using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            var sql = "SELECT filename, author FROM documents WHERE id = @id";
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", fileId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                        baseDoc = new DocumentVersion
                        {
                            Filename = reader.GetString(0),
                            Author = reader.GetString(1)
                        };
                }
            }

            if (baseDoc == null) return new List<DocumentVersion>();

            var versionSql =
                "SELECT id, filename, author, version, upload_timestamp FROM documents WHERE filename = @filename AND author = @author ORDER BY version DESC";
            using (var cmd2 = new NpgsqlCommand(versionSql, conn))
            {
                cmd2.Parameters.AddWithValue("filename", baseDoc.Filename);
                cmd2.Parameters.AddWithValue("author", baseDoc.Author);
                using (var reader2 = await cmd2.ExecuteReaderAsync())
                {
                    var list = new List<DocumentVersion>();
                    while (await reader2.ReadAsync())
                        list.Add(new DocumentVersion
                        {
                            Id = reader2.GetInt32(0),
                            Filename = reader2.GetString(1),
                            Author = reader2.GetString(2),
                            Version = reader2.GetInt32(3),
                            UploadTimestamp = reader2.GetDateTime(4)
                        });

                    return list;
                }
            }
        }
    }


    public async Task<List<DocumentVersion>> GetAllDocuments()
    {
        var list = new List<DocumentVersion>();
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = "SELECT id, filename, author, version, upload_timestamp FROM documents ORDER BY id ASC";
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(new DocumentVersion
            {
                Id = reader.GetInt32(0),
                Filename = reader.GetString(1),
                Author = reader.GetString(2),
                Version = reader.GetInt32(3),
                UploadTimestamp = reader.GetDateTime(4)
            });

        return list;
    }
}