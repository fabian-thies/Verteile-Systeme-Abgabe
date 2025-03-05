// ChatHub.cs
// English comment: SignalR Hub that delegates authentication operations to the AuthService.

using Microsoft.AspNetCore.SignalR;
using Server.Services;

namespace Server.Hubs;

public class ChatHub : Hub
{
    private readonly IAuthService _authService;
    private readonly ILogger<ChatHub> _logger;

    // English comment: Constructor that injects the AuthService and logger.
    public ChatHub(IAuthService authService, ILogger<ChatHub> logger)
    {
        _authService = authService;
        _logger = logger;
        _logger.LogInformation("ChatHub initialized.");
    }

    // English comment: Delegates the login request to the AuthService.
    public async Task<bool> Login(string username, string password)
    {
        return await _authService.Login(username, password);
    }

    // English comment: Delegates the registration request to the AuthService.
    public async Task<bool> Register(string username, string password)
    {
        return await _authService.Register(username, password);
    }
}