// Services/PluginManager.cs
using ChatFileApp.Models.Plugins;
using System.Reflection;
using System.Security.Cryptography;

namespace ChatFileApp.Services
{
    public class PluginManager
    {
        private readonly Dictionary<string, IPlugin> _loadedPlugins = new();
        private readonly Dictionary<string, PluginMetadata> _availablePlugins = new();
        private readonly ILogger<PluginManager> _logger;
        private readonly IConfiguration _configuration;

        public PluginManager(ILogger<PluginManager> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            _logger.LogInformation($"Testing: {configuration["PluginVerificationKey"]}");
        }

        public async Task InitializeAsync()
        {
            // Load plugin metadata from a predefined directory
            var pluginsDirectory = _configuration["PluginsDirectory"] ?? "Plugins";
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

            var test = Directory.GetFiles("C:\\Users\\lomic\\Downloads\\Verteile-Systeme Abgabe\\ChatFileApp\\Models\\Plugins").ToList();
            
            _logger.LogInformation("GetFiles: {test}", test);
            
            foreach (var metadataFile in Directory.GetFiles(pluginsDirectory, "*.metadata.json"))
            {
                try
                {
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<PluginMetadata>(
                        await File.ReadAllTextAsync(metadataFile));
                    if (metadata != null && VerifyPluginSignature(metadata))
                    {
                        _logger.LogInformation("Loaded plugin metadata: {Path}", metadataFile);
                        _availablePlugins[metadata.Id] = metadata;
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
            _logger.LogInformation($"Loaded plugins: {string.Join(", ", _availablePlugins.Keys)}");
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
                // Load plugin with restricted permissions
                var loadContext = new PluginLoadContext(metadata.AssemblyPath);
                var assembly = loadContext.LoadFromAssemblyPath(metadata.AssemblyPath);
                
                var pluginType = assembly.GetType(metadata.EntryPoint);
                if (pluginType == null) 
                {
                    throw new TypeLoadException($"Entry point {metadata.EntryPoint} not found in plugin {pluginId}");
                }
                
                var plugin = Activator.CreateInstance(pluginType) as IPlugin;
                if (plugin == null)
                {
                    throw new InvalidCastException($"Entry point {metadata.EntryPoint} does not implement IPlugin");
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

        private bool VerifyPluginSignature(PluginMetadata metadata)
        {
            // TODO: Add certificate verification
            return true;
            try
            {
                // Verify plugin's digital signature against trusted certificates
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
        
        public IPlugin GetLoadedPlugin(string pluginId) => 
            _loadedPlugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
    }
}