using Microsoft.AspNetCore.SignalR;
using Server.Services;
using System.Collections.Concurrent;

namespace Server.Hubs;

public class ChatHub : Hub
{
    private readonly IAuthService _authService;
    private readonly ILogger<ChatHub> _logger;
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
            _logger.LogInformation("Private message from {Sender} to {TargetUser}: {Message}", sender, targetUser, message);
            await Clients.Group(targetUser).SendAsync("ReceivePrivateMessage", sender, message);
        }
    }

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

        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", "[System]", $"{username} has joined the group {groupName}.");
    }

    public async Task LeaveGroup(string groupName)
    {
        var username = _userConnections.GetValueOrDefault(Context.ConnectionId);
        if (string.IsNullOrEmpty(username))
        {
            _logger.LogWarning("LeaveGroup called but username is null for connection {ConnectionId}", Context.ConnectionId);
            return;
        }

        _logger.LogInformation("{User} leaving group {GroupName}", username, groupName);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", "[System]", $"{username} has left the group {groupName}.");
    }

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
        if (_userConnections.TryRemove(Context.ConnectionId, out var username))
        {
            await Clients.All.SendAsync("ReceiveSystemMessage", $"{username} has logged out.");
        }
        await base.OnDisconnectedAsync(exception);
    }
}
