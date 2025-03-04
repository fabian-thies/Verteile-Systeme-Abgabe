// Hubs/PluginHub.cs
using ChatFileApp.Services;
using Microsoft.AspNetCore.SignalR;

namespace ChatFileApp.Hubs
{
    public class PluginHub : Hub
    {
        private readonly PluginManager _pluginManager;
        private readonly ILogger<PluginHub> _logger;

        public PluginHub(PluginManager pluginManager, ILogger<PluginHub> logger)
        {
            _pluginManager = pluginManager;
            _logger = logger;
        }

        public async Task RequestPluginActivation(string pluginId, string targetUserId)
        {
            var plugin = _pluginManager.GetAvailablePlugins().FirstOrDefault(p => p.Id == pluginId);
            if (plugin == null)
            {
                throw new HubException($"Plugin {pluginId} not found");
            }

            // Notify target user about plugin request
            await Clients.User(targetUserId).SendAsync("PluginActivationRequested", 
                Context.UserIdentifier, pluginId, plugin.Name, plugin.Description);
        }

        public async Task AcceptPluginActivation(string pluginId, string requestingUserId)
        {
            try
            {
                // Load the plugin
                await _pluginManager.LoadPluginAsync(pluginId);
                
                // Get plugin metadata for client-side initialization
                var plugin = _pluginManager.GetAvailablePlugins().FirstOrDefault(p => p.Id == pluginId);
                
                // Notify both users that plugin is ready
                await Clients.User(requestingUserId).SendAsync("PluginActivated", pluginId);
                await Clients.Caller.SendAsync("PluginActivated", pluginId);
                
                _logger.LogInformation("Plugin {PluginId} activated between users {User1} and {User2}", 
                    pluginId, requestingUserId, Context.UserIdentifier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate plugin {PluginId}", pluginId);
                await Clients.Caller.SendAsync("PluginActivationFailed", pluginId, ex.Message);
                await Clients.User(requestingUserId).SendAsync("PluginActivationFailed", pluginId, ex.Message);
            }
        }
    }
}