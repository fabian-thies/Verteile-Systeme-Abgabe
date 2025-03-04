// Services/PluginManager.cs

using ChatFileApp.Models.Plugins;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.FileProviders;

namespace ChatFileApp.Services
{
    public class PluginManager
    {
        private readonly Dictionary<string, IPlugin> _loadedPlugins = new();
        private readonly Dictionary<string, PluginMetadata> _availablePlugins = new();
        private readonly ILogger<PluginManager> _logger;
        private readonly IConfiguration _configuration;

        // We store the ApplicationPartManager and IHostEnvironment to register new parts and static files
        private readonly ApplicationPartManager _applicationPartManager;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IServiceProvider _serviceProvider;

        public PluginManager(
            ILogger<PluginManager> logger,
            IConfiguration configuration,
            ApplicationPartManager mgrFromRazor,
            IHostEnvironment hostEnvironment,
            IServiceProvider serviceProvider
        )
        {
            _logger = logger;
            _configuration = configuration;
            _applicationPartManager = mgrFromRazor; // same manager from Razor
            _hostEnvironment = hostEnvironment;
            _serviceProvider = serviceProvider;
        }

        public async Task InitializeAsync()
        {
            // Load plugin metadata from a predefined directory
            var pluginsDirectory = _configuration["PluginsDirectory"] ?? "Plugins";
            // For demo, override path (adapt as needed!)
            pluginsDirectory = "C:\\Users\\lomic\\Downloads\\Verteile-Systeme Abgabe\\ChatFileApp\\Models\\Plugins";

            if (!Directory.Exists(pluginsDirectory))
            {
                _logger.LogInformation("Creating plugins directory: {Directory}", pluginsDirectory);
                Directory.CreateDirectory(pluginsDirectory);
            }
            else
            {
                _logger.LogInformation("Loading plugins from: {Directory}", pluginsDirectory);
            }

            var metadataFiles = Directory.GetFiles(pluginsDirectory, "*.metadata.json");
            foreach (var metadataFile in metadataFiles)
            {
                try
                {
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<PluginMetadata>(
                        await File.ReadAllTextAsync(metadataFile)
                    );
                    
                    if (metadata != null && VerifyPluginSignature(metadata))
                    {
                        _availablePlugins[metadata.Id] = metadata;
                        _logger.LogInformation("Loaded plugin metadata: {Path}", metadataFile);
                    }
                    else
                    {
                        _logger.LogWarning("Invalid plugin metadata or signature: {Path}", metadataFile);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load plugin metadata: {Path}", metadataFile);
                }
            }
            _logger.LogInformation("Loaded plugins: {0}", string.Join(", ", _availablePlugins.Keys));
        }

        public async Task<IPlugin> LoadPluginAsync(string pluginId)
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var loadedPlugin))
            {
                return loadedPlugin;
            }

            if (!_availablePlugins.TryGetValue(pluginId, out var metadata))
            {
                throw new KeyNotFoundException($"Plugin with ID {pluginId} not found.");
            }

            try
            {
                // Create load context and load assembly
                var loadContext = new PluginLoadContext(metadata.AssemblyPath);
                var assembly = loadContext.LoadFromAssemblyPath(metadata.AssemblyPath);

                // Dynamically add new pages/controllers to the application
                _applicationPartManager.ApplicationParts.Add(new AssemblyPart(assembly));

                // If there are plugin-specific static files, register them
                RegisterStaticFiles(pluginId, assembly);

                var pluginType = assembly.GetType(metadata.EntryPoint);
                if (pluginType == null)
                {
                    throw new TypeLoadException(
                        $"Entry point {metadata.EntryPoint} not found in plugin {pluginId}"
                    );
                }

                var plugin = Activator.CreateInstance(pluginType) as IPlugin;
                if (plugin == null)
                {
                    throw new InvalidCastException(
                        $"Entry point {metadata.EntryPoint} does not implement IPlugin"
                    );
                }

                await plugin.Initialize();
                _loadedPlugins[pluginId] = plugin;
                return plugin;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin: {PluginId}", pluginId);
                throw;
            }
        }

        // Example to register static assets from a hypothetical plugin folder
        private void RegisterStaticFiles(string pluginId, Assembly pluginAssembly)
        {
            // Here we assume the plugin might have a "wwwroot" folder next to its DLL
            // For example: <PluginsFolder>/<PluginName>/wwwroot
            // We'll try to register that as a new static file source

            try
            {
                var pluginDirectory = Path.GetDirectoryName(pluginAssembly.Location);
                if (pluginDirectory == null) return;

                var wwwRootPath = Path.Combine(pluginDirectory, "wwwroot");
                if (!Directory.Exists(wwwRootPath)) return;

                // We can store all the mappings in a custom collection so we can .Map() them in Program.cs, if needed
                // Alternatively, we can create a specialized middleware pipeline:
                var fileProvider = new PhysicalFileProvider(wwwRootPath);

                // We register it in a global static dictionary for later usage in 'MapStaticAssets'
                StaticAssetsMappings.PluginStaticMappings[pluginId] = fileProvider;

                _logger.LogInformation("Registered static file provider for plugin '{PluginId}'", pluginId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register static files for plugin '{PluginId}'", pluginId);
            }
        }

        private bool VerifyPluginSignature(PluginMetadata metadata)
        {
            // TODO: Add real certificate verification if needed
            return true;
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(_configuration["PluginVerificationKey"]);

                var pluginData = File.ReadAllBytes(metadata.AssemblyPath);
                return rsa.VerifyData(pluginData, metadata.Signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify plugin signature: {PluginId}", metadata.Id);
                return false;
            }
        }

        public IEnumerable<PluginMetadata> GetAvailablePlugins() => _availablePlugins.Values;

        public IPlugin GetLoadedPlugin(string pluginId)
        {
            return _loadedPlugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
        }
    }

    // Utility class that keeps track of plugin static file providers so we can map them in Program.cs
    public static class StaticAssetsMappings
    {
        public static Dictionary<string, IFileProvider> PluginStaticMappings { get; } 
            = new Dictionary<string, IFileProvider>();
    }
}
