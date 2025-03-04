using System.Security.Claims;
using ChatFileApp.Services;
using ChatFileApp.Models.Plugins;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatFileApp.Pages
{
    public class PluginsModel : PageModel
    {
        private readonly PluginManager _pluginManager;
        
        public string CurrentUserId { get; set; }

        // Constructor injection of the PluginManager
        public PluginsModel(PluginManager pluginManager)
        {
            _pluginManager = pluginManager;
        }

        // List of available plugins retrieved from the PluginManager
        public List<PluginMetadata> AvailablePlugins { get; set; } = new List<PluginMetadata>();

        public void OnGet()
        {
            // Retrieve available plugins and convert to a list for display
            AvailablePlugins = _pluginManager.GetAvailablePlugins().ToList();
            
            CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Not authenticated";
        }
    }
}