using Microsoft.AspNetCore.SignalR;
using Server.Services;
using System.Collections.Concurrent;

namespace Server.Hubs;

public class ChatHub : Hub
{
    private readonly IAuthService _authService;
    private readonly ILogger<ChatHub> _logger;
    // Mapping of connection IDs to usernames for tracking online users
    private static ConcurrentDictionary<string, string> _userConnections = new ConcurrentDictionary<string, string>();

    public ChatHub(IAuthService authService, ILogger<ChatHub> logger)
    {
        _authService = authService;
        _logger = logger;
        _logger.LogInformation("ChatHub initialized.");
    }

    public async Task<bool> Login(string username, string password)
    {
        bool isAuthenticated = await _authService.Login(username, password);
        if (isAuthenticated)
        {
            // Store the username for the current connection
            _userConnections[Context.ConnectionId] = username;
            // Add the connection to a group named as the username for private messaging
            await Groups.AddToGroupAsync(Context.ConnectionId, username);
        }
        return isAuthenticated;
    }

    public async Task<bool> Register(string username, string password)
    {
        return await _authService.Register(username, password);
    }

    // Method to send a private message to a specific user
    public async Task SendPrivateMessage(string targetUser, string message)
    {
        if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
        {
            _logger.LogInformation("Private message from {Sender} to {TargetUser}: {Message}", sender, targetUser, message);
            // Send the message to the group corresponding to the target user
            await Clients.Group(targetUser).SendAsync("ReceivePrivateMessage", sender, message);
        }
    }

    // Method to join a group chat
    public async Task JoinGroup(string groupName)
    {
        var username = _userConnections.GetValueOrDefault(Context.ConnectionId);
        if (string.IsNullOrEmpty(username))
        {
            _logger.LogWarning("JoinGroup called but username is null for connection {ConnectionId}", Context.ConnectionId);
            return;
        }
    
        _logger.LogInformation("{User} joining group {GroupName}", username, groupName);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    // Method to leave a group chat
    public async Task LeaveGroup(string groupName)
    {
        _logger.LogInformation("{User} leaving group {GroupName}", _userConnections.GetValueOrDefault(Context.ConnectionId), groupName);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    // Method to send a message to a group
    public async Task SendGroupMessage(string groupName, string message)
    {
        if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
        {
            _logger.LogInformation("Group message from {Sender} in {GroupName}: {Message}", sender, groupName, message);
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", sender, message);
        }
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        // Remove the user mapping when the connection is closed
        _userConnections.TryRemove(Context.ConnectionId, out _);
        await base.OnDisconnectedAsync(exception);
    }
}
