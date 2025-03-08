using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using Server.Services;
using Shared.Models;

namespace Server.Hubs;

public class ChatHub : Hub
{
    private static ConcurrentDictionary<string, HashSet<string>> _groups =
        new ConcurrentDictionary<string, HashSet<string>>();

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

        _logger.LogInformation("ChatHub initialized.");
    }

    public async Task<bool> Login(string username, string password)
    {
        _logger.LogInformation("Login attempt for user: {Username}", username);
        var isAuthenticated = await _authService.Login(username, password);
        if (isAuthenticated)
        {
            _userConnections[Context.ConnectionId] = username;
            await Groups.AddToGroupAsync(Context.ConnectionId, username);
            await Clients.All.SendAsync("ReceiveSystemMessage", username + " has logged in.");
            _logger.LogInformation("User {Username} logged in successfully. Connection ID {ConnectionId} added.",
                username, Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning("Login failed for user: {Username}", username);
        }

        return isAuthenticated;
    }

    public async Task<bool> Register(string username, string password)
    {
        _logger.LogInformation("Registration attempt for user: {Username}", username);
        var result = await _authService.Register(username, password);
        if (result)
        {
            _logger.LogInformation("User {Username} registered successfully.", username);
        }
        else
        {
            _logger.LogWarning("Registration failed for user: {Username}", username);
        }

        return result;
    }

    public async Task SendPrivateMessage(string targetUser, string message)
    {
        if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
        {
            _logger.LogInformation("User {Sender} sending private message to {TargetUser}: {Message}", sender,
                targetUser, message);
            await Clients.Group(targetUser).SendAsync("ReceivePrivateMessage", sender, message);
        }
        else
        {
            _logger.LogWarning("SendPrivateMessage: No sender found for connection ID: {ConnectionId}",
                Context.ConnectionId);
        }
    }

    public async Task JoinGroup(string groupName)
    {
        var username = _userConnections.GetValueOrDefault(Context.ConnectionId);
        if (string.IsNullOrEmpty(username))
        {
            _logger.LogWarning("JoinGroup: Username not found for connection ID: {ConnectionId}", Context.ConnectionId);
            return;
        }

        // Check if the user is already in the group to prevent duplicate join messages
        if (_groups.TryGetValue(groupName, out var members) && members.Contains(Context.ConnectionId))
        {
            _logger.LogInformation("User {Username} is already in group {GroupName}", username, groupName);
            return;
        }

        _logger.LogInformation("User {Username} joining group {GroupName}", username, groupName);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        // Update group membership
        _groups.AddOrUpdate(groupName,
            new HashSet<string> { Context.ConnectionId },
            (key, existing) =>
            {
                existing.Add(Context.ConnectionId);
                return existing;
            });

        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", "[System]",
            username + " has joined the group " + groupName + ".");
        await BroadcastGroupList();
    }

    public async Task LeaveGroup(string groupName)
    {
        var username = _userConnections.GetValueOrDefault(Context.ConnectionId);
        if (string.IsNullOrEmpty(username))
        {
            _logger.LogWarning("LeaveGroup: Username not found for connection ID: {ConnectionId}",
                Context.ConnectionId);
            return;
        }

        _logger.LogInformation("User {Username} leaving group {GroupName}", username, groupName);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        // Update group membership
        if (_groups.TryGetValue(groupName, out var members))
        {
            members.Remove(Context.ConnectionId);
            if (members.Count == 0)
            {
                _groups.TryRemove(groupName, out _);
            }
        }

        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", "[System]",
            username + " has left the group " + groupName + ".");
        await BroadcastGroupList();
    }

    private async Task BroadcastGroupList()
    {
        var groupList = _groups.Keys.ToList();
        await Clients.All.SendAsync("ReceiveGroupList", groupList);
    }

    public Task<List<string>> GetOpenGroups()
    {
        var groupList = _groups.Keys.ToList();
        return Task.FromResult(groupList);
    }

    public async Task SendGroupMessage(string groupName, string message)
    {
        if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
        {
            _logger.LogInformation("User {Sender} sending group message to {GroupName}: {Message}", sender, groupName,
                message);
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", sender, message);
        }
        else
        {
            _logger.LogWarning("SendGroupMessage: No sender found for connection ID: {ConnectionId}",
                Context.ConnectionId);
        }
    }

    public async Task<int> UploadDocument(string filename, string base64Content, string author, string metadata)
    {
        if (!_userConnections.TryGetValue(Context.ConnectionId, out var a))
        {
            _logger.LogWarning("UploadDocument: No user found for connection ID: {ConnectionId}", Context.ConnectionId);
            return -1;
        }

        _logger.LogInformation("User {User} uploading document: {Filename}", a, filename);
        var documentId = await _fileService.UploadFileAsync(filename, base64Content, a, metadata);
        await Clients.All.SendAsync("ReceiveSystemMessage", a + " uploaded document " + filename + ".");
        _logger.LogInformation("Document {Filename} uploaded by {User} with Document ID: {DocumentId}", filename, a,
            documentId);
        return documentId;
    }

    public async Task<string> DownloadDocument(int documentId)
    {
        _logger.LogInformation("DownloadDocument requested for Document ID: {DocumentId}", documentId);
        var fileInfo = await _fileService.DownloadFileInfoAsync(documentId);
        if (fileInfo == null)
        {
            _logger.LogWarning("DownloadDocument: No file found for Document ID: {DocumentId}", documentId);
            return null;
        }

        _logger.LogInformation("Document {DocumentId} downloaded successfully.", documentId);
        return JsonSerializer.Serialize(fileInfo);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        if (_userConnections.TryRemove(Context.ConnectionId, out var username))
        {
            _logger.LogInformation("User {Username} disconnected. Connection ID {ConnectionId} removed.", username,
                Context.ConnectionId);

            // Remove disconnected user from all groups
            foreach (var group in _groups.Keys)
            {
                if (_groups.TryGetValue(group, out var members))
                {
                    if (members.Remove(Context.ConnectionId) && members.Count == 0)
                    {
                        _groups.TryRemove(group, out _);
                    }
                }
            }

            await BroadcastGroupList();

            await Clients.All.SendAsync("ReceiveSystemMessage", username + " has logged out.");
        }
        else
        {
            _logger.LogWarning("OnDisconnectedAsync: Connection ID {ConnectionId} was not found in user connections.",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

        public async Task SendWhiteboardLine(string target, bool isGroup, double x1, double y1, double x2, double y2)
    {
        _logger.LogInformation("Sending whiteboard line to group {Target}: [{X1}, {Y1}] to [{X2}, {Y2}]", target, x1, y1, x2, y2);
        await Clients.Group(target).SendAsync("ReceiveWhiteboardLine", x1, y1, x2, y2);
    }

    public async Task SendWhiteboardLineBroadcast(double x1, double y1, double x2, double y2)
    {
        _logger.LogInformation("Broadcasting whiteboard line: [{X1}, {Y1}] to [{X2}, {Y2}]", x1, y1, x2, y2);
        await Clients.All.SendAsync("ReceiveWhiteboardLine", x1, y1, x2, y2);
    }

    public async Task RequestWhiteboardPlugin(string targetUser)
    {
        if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
        {
            _logger.LogInformation("User {Sender} requested whiteboard plugin for target {TargetUser}", sender, targetUser);
            await Clients.Group(targetUser).SendAsync("ReceiveWhiteboardPluginRequest", sender);
        }
        else
        {
            _logger.LogWarning("RequestWhiteboardPlugin: No sender found for connection ID: {ConnectionId}", Context.ConnectionId);
        }
    }

    public async Task SendPluginFile(string targetUser, string base64PluginContent)
    {
        if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
        {
            _logger.LogInformation("User {Sender} sending plugin file to {TargetUser}", sender, targetUser);
            await Clients.Group(targetUser).SendAsync("ReceivePluginFile", sender, base64PluginContent);
        }
        else
        {
            _logger.LogWarning("SendPluginFile: No sender found for connection ID: {ConnectionId}", Context.ConnectionId);
        }
    }

    public async Task RequestPluginFile(string requester)
    {
        if (_userConnections.TryGetValue(Context.ConnectionId, out var requestingUser))
        {
            _logger.LogInformation("User {RequestingUser} requested plugin file for requester {Requester}", requestingUser, requester);
            await Clients.Group(requester).SendAsync("ReceivePluginFileRequest", requestingUser);
        }
        else
        {
            _logger.LogWarning("RequestPluginFile: No requesting user found for connection ID: {ConnectionId}", Context.ConnectionId);
        }
    }
    
    public async Task<List<DocumentVersion>> GetDocumentVersionsById(int fileId)
    {
        _logger.LogInformation("Fetching document versions for File ID: {FileId}", fileId);
        DocumentVersion baseDoc = null;
        using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            _logger.LogInformation("Database connection opened for GetDocumentVersionsById.");

            var sql = "SELECT filename, author FROM documents WHERE id = @id";
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", fileId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        baseDoc = new DocumentVersion
                        {
                            Filename = reader.GetString(0),
                            Author = reader.GetString(1)
                        };
                        _logger.LogInformation("Base document found for File ID: {FileId}. Filename: {Filename}, Author: {Author}", fileId, baseDoc.Filename, baseDoc.Author);
                    }
                }
            }

            if (baseDoc == null)
            {
                _logger.LogWarning("No document found for File ID: {FileId}", fileId);
                return new List<DocumentVersion>();
            }

            var versionSql = "SELECT id, filename, author, version, upload_timestamp FROM documents WHERE filename = @filename AND author = @author ORDER BY version DESC";
            using (var cmd2 = new NpgsqlCommand(versionSql, conn))
            {
                cmd2.Parameters.AddWithValue("filename", baseDoc.Filename);
                cmd2.Parameters.AddWithValue("author", baseDoc.Author);
                using (var reader2 = await cmd2.ExecuteReaderAsync())
                {
                    var list = new List<DocumentVersion>();
                    while (await reader2.ReadAsync())
                    {
                        list.Add(new DocumentVersion
                        {
                            Id = reader2.GetInt32(0),
                            Filename = reader2.GetString(1),
                            Author = reader2.GetString(2),
                            Version = reader2.GetInt32(3),
                            UploadTimestamp = reader2.GetDateTime(4)
                        });
                    }
                    _logger.LogInformation("Retrieved {Count} versions for File ID: {FileId}", list.Count, fileId);
                    return list;
                }
            }
        }
    }
    
    public async Task<List<DocumentVersion>> GetAllDocuments()
    {
        _logger.LogInformation("Fetching all documents.");
        var list = new List<DocumentVersion>();
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        _logger.LogInformation("Database connection opened for GetAllDocuments.");

        var sql = "SELECT id, filename, author, version, upload_timestamp FROM documents ORDER BY id ASC";
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
        _logger.LogInformation("Total documents retrieved: {Count}", list.Count);
        return list;
    }
}