using Microsoft.AspNetCore.SignalR;
using Server.Services;

namespace Server.Hubs;

public class ChatHub : Hub
{
    private readonly IAuthService _authService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IAuthService authService, ILogger<ChatHub> logger)
    {
        _authService = authService;
        _logger = logger;
        _logger.LogInformation("ChatHub initialized.");
    }

    public async Task<bool> Login(string username, string password)
    {
        return await _authService.Login(username, password);
    }

    public async Task<bool> Register(string username, string password)
    {
        return await _authService.Register(username, password);
    }
}