using System.IO;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using System.Windows;

public class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    public IPlugin[] LoadPlugins(string pluginDirectory)
    {
        _logger.LogInformation("Loading plugins from directory: {directory}", pluginDirectory);

        if (!Directory.Exists(pluginDirectory))
        {
            _logger.LogError("Plugin directory does not exist: {directory}", pluginDirectory);
            return Array.Empty<IPlugin>();
        }

        var plugins = Directory.GetFiles(pluginDirectory, "*.dll")
            .SelectMany(file =>
            {
                try
                {
                    if (!VerifyPlugin(file))
                    {
                        _logger.LogWarning("User declined to load plugin from file: {file}", file);
                        return Enumerable.Empty<IPlugin>();
                    }

                    var alc = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(file), true);
                    var assembly = alc.LoadFromAssemblyPath(Path.GetFullPath(file));

                    var allTypes = assembly.GetTypes();
                    foreach (var type in allTypes)
                        _logger.LogDebug("Found type in assembly {file}: {TypeName}", file, type.FullName);

                    var pluginTypes = allTypes
                        .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);

                    foreach (var pluginType in pluginTypes)
                        _logger.LogDebug("Instantiating plugin type: {PluginType}", pluginType.FullName);

                    return pluginTypes.Select(t => (IPlugin)Activator.CreateInstance(t));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading plugin from file: {file}", file);
                    return Enumerable.Empty<IPlugin>();
                }
            })
            .ToArray();

        _logger.LogInformation("Loaded {count} plugins.", plugins.Length);
        return plugins;
    }

    private bool VerifyPlugin(string pluginFile)
    {
        _logger.LogInformation("Verifying plugin: {file}", pluginFile);
        try
        {
            using (var stream = File.OpenRead(pluginFile))
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(stream);
                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                var allowedHashes = new HashSet<string>
                {
                    "f972dafa8ee050a130ae6f394cae7544b87b77e2648ab9cdeba82cc5ba51693b" // ModerationPlugin
                };

                if (allowedHashes.Contains(hashString))
                {
                    _logger.LogInformation("Plugin {file} is trusted. Hash: {hash}", pluginFile, hashString);
                    MessageBox.Show($"Plugin {Path.GetFileName(pluginFile)} is trusted.", "Plugin Verification",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Plugin {file} is not trusted. Hash: {hash}", pluginFile, hashString);
                    var result = MessageBox.Show(
                        $"Plugin {Path.GetFileName(pluginFile)} is not trusted (Hash: {hashString}).\nDo you want to load it anyway?",
                        "Plugin Verification",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        _logger.LogWarning("User chose to load the untrusted plugin: {file}", pluginFile);
                        return true;
                    }
                    else
                    {
                        _logger.LogInformation("User declined to load the untrusted plugin: {file}", pluginFile);
                        return false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin verification for file: {file}", pluginFile);
            return false;
        }
    }
}