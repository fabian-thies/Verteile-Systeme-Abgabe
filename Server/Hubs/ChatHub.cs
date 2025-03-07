using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using Server.Services;
using Shared.Models;

namespace Server.Hubs;

public class ChatHub : Hub
{
    // Dictionary to track groups and their connected connection IDs
    private static ConcurrentDictionary<string, HashSet<string>> _groups = new ConcurrentDictionary<string, HashSet<string>>();

    private static readonly ConcurrentDictionary<string, string> _userConnections = new();

    private readonly IAuthService _authService;
    private readonly string _connectionString;
    private readonly IFileManagementService _fileService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IAuthService authService, ILogger<ChatHub> logger, IFileManagementService fileService, IConfiguration config)
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
            _logger.LogInformation("User {Username} logged in successfully. Connection ID {ConnectionId} added.", username, Context.ConnectionId);
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
            _logger.LogInformation("User {Sender} sending private message to {TargetUser}: {Message}", sender, targetUser, message);
            await Clients.Group(targetUser).SendAsync("ReceivePrivateMessage", sender, message);
        }
        else
        {
            _logger.LogWarning("SendPrivateMessage: No sender found for connection ID: {ConnectionId}", Context.ConnectionId);
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
            (key, existing) => { existing.Add(Context.ConnectionId); return existing; });

        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", "[System]", username + " has joined the group " + groupName + ".");
        await BroadcastGroupList();
    }

    public async Task LeaveGroup(string groupName)
    {
        var username = _userConnections.GetValueOrDefault(Context.ConnectionId);
        if (string.IsNullOrEmpty(username))
        {
            _logger.LogWarning("LeaveGroup: Username not found for connection ID: {ConnectionId}", Context.ConnectionId);
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

        await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", "[System]", username + " has left the group " + groupName + ".");
        await BroadcastGroupList();
    }

    // Method to broadcast current open groups to all clients
    private async Task BroadcastGroupList()
    {
        var groupList = _groups.Keys.ToList();
        await Clients.All.SendAsync("ReceiveGroupList", groupList);
    }

    // Method to get open groups (for initial load)
    public Task<List<string>> GetOpenGroups()
    {
        var groupList = _groups.Keys.ToList();
        return Task.FromResult(groupList);
    }

    public async Task SendGroupMessage(string groupName, string message)
    {
        if (_userConnections.TryGetValue(Context.ConnectionId, out var sender))
        {
            _logger.LogInformation("User {Sender} sending group message to {GroupName}: {Message}", sender, groupName, message);
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", sender, message);
        }
        else
        {
            _logger.LogWarning("SendGroupMessage: No sender found for connection ID: {ConnectionId}", Context.ConnectionId);
        }
    }

    // ... (weitere Methoden bleiben unverändert) ...

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        if (_userConnections.TryRemove(Context.ConnectionId, out var username))
        {
            _logger.LogInformation("User {Username} disconnected. Connection ID {ConnectionId} removed.", username, Context.ConnectionId);

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
            _logger.LogWarning("OnDisconnectedAsync: Connection ID {ConnectionId} was not found in user connections.", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
