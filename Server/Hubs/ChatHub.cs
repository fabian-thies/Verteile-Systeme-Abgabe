// File: Server/Hubs/ChatHub.cs

using Microsoft.AspNetCore.SignalR;
using Server.Services;
using System.Collections.Concurrent;

namespace Server.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IAuthService _authService;
        private readonly ILogger<ChatHub> _logger;
        private readonly IFileManagementService _fileService;

        private static ConcurrentDictionary<string, string> _userConnections =
            new ConcurrentDictionary<string, string>();

        public ChatHub(IAuthService authService, ILogger<ChatHub> logger, IFileManagementService fileService)
        {
            _authService = authService;
            _fileService = fileService;
            _logger = logger;
            _logger.LogInformation("ChatHub initialized.");
        }

        public async Task<bool> Login(string username, string password)
        {
            bool isAuthenticated = await _authService.Login(username, password);
            if (isAuthenticated)
            {
                _userConnections[Context.ConnectionId] = username;
                await Groups.AddToGroupAsync(Context.ConnectionId, username);
                await Clients.All.SendAsync("ReceiveSystemMessage", $"{username} has logged in.");
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
                _logger.LogInformation("Private message from {Sender} to {TargetUser}: {Message}", sender, targetUser,
                    message);
                await Clients.Group(targetUser).SendAsync("ReceivePrivateMessage", sender, message);
            }
        }

        public async Task JoinGroup(string groupName)
        {
            var username = _userConnections.GetValueOrDefault(Context.ConnectionId);
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("JoinGroup called but username is null for connection {ConnectionId}",
                    Context.ConnectionId);
                return;
            }

            _logger.LogInformation("{User} joining group {GroupName}", username, groupName);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", "[System]",
                $"{username} has joined the group {groupName}.");
        }

        public async Task LeaveGroup(string groupName)
        {
            var username = _userConnections.GetValueOrDefault(Context.ConnectionId);
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("LeaveGroup called but username is null for connection {ConnectionId}",
                    Context.ConnectionId);
                return;
            }

            _logger.LogInformation("{User} leaving group {GroupName}", username, groupName);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", "[System]",
                $"{username} has left the group {groupName}.");
        }

        public async Task SendGroupMessage(string groupName, string message)
        {
            if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
            {
                _logger.LogInformation("Group message from {Sender} in {GroupName}: {Message}", sender, groupName,
                    message);
                await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", sender, message);
            }
        }

        public async Task SendWhiteboardLine(string target, bool isGroup, double x1, double y1, double x2, double y2)
        {
            _logger.LogInformation(
                "Whiteboard line drawn for target {target} (isGroup: {isGroup}): ({x1}, {y1}) to ({x2}, {y2})", target,
                isGroup, x1, y1, x2, y2);
            await Clients.Group(target).SendAsync("ReceiveWhiteboardLine", x1, y1, x2, y2);
        }

        public async Task SendWhiteboardLineBroadcast(double x1, double y1, double x2, double y2)
        {
            _logger.LogInformation("Whiteboard line drawn (broadcast): ({x1}, {y1}) to ({x2}, {y2})", x1, y1, x2, y2);
            await Clients.All.SendAsync("ReceiveWhiteboardLine", x1, y1, x2, y2);
        }

        public async Task RequestWhiteboardPlugin(string targetUser)
        {
            if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
            {
                _logger.LogInformation("User {Sender} is requesting to send the whiteboard plugin to {TargetUser}.",
                    sender, targetUser);
                await Clients.Group(targetUser).SendAsync("ReceiveWhiteboardPluginRequest", sender);
            }
        }

        // Method for sending the plugin file content (as Base64 string) to the target user.
        public async Task SendPluginFile(string targetUser, string base64PluginContent)
        {
            if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
            {
                _logger.LogInformation("Sending plugin file from {Sender} to {TargetUser}.", sender, targetUser);
                await Clients.Group(targetUser).SendAsync("ReceivePluginFile", sender, base64PluginContent);
            }
        }

        // New method: Forwards plugin file requests from a client (which does not have the plugin)
        // to the client that owns the plugin.
        public async Task RequestPluginFile(string requester)
        {
            if (_userConnections.TryGetValue(Context.ConnectionId, out var requestingUser))
            {
                // Forward the request to the group of the requester (plugin owner)
                await Clients.Group(requester).SendAsync("ReceivePluginFileRequest", requestingUser);
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (_userConnections.TryRemove(Context.ConnectionId, out var username))
            {
                await Clients.All.SendAsync("ReceiveSystemMessage", $"{username} has logged out.");
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task<int> UploadDocument(string filename, string base64Content, string metadata)
        {
            if (!_userConnections.TryGetValue(Context.ConnectionId, out var author))
            {
                _logger.LogWarning("Unauthenticated upload attempt.");
                return -1; // Or throw an exception/return a specific error code.
            }

            int documentId = await _fileService.UploadFileAsync(filename, base64Content, author, metadata);
            await Clients.All.SendAsync("ReceiveSystemMessage", $"{author} uploaded document {filename}.");
            return documentId;
        }

        // Method for downloading a document.
        public async Task<string> DownloadDocument(int documentId)
        {
            string base64Content = await _fileService.DownloadFileAsync(documentId);
            return base64Content;
        }
    }
}