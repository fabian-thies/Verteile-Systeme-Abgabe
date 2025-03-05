// PluginLoader.cs

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

public class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    // Loads all plugins from the specified directory.
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
                    // Verify plugin signature before loading.
                    if (!VerifyPlugin(file))
                    {
                        _logger.LogWarning("Plugin verification failed for file: {file}", file);
                        return Enumerable.Empty<IPlugin>();
                    }

                    // Create a new AssemblyLoadContext for plugin isolation.
                    var alc = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(file), true);
                    var assembly = alc.LoadFromAssemblyPath(Path.GetFullPath(file));

                    // Log all types found in the assembly.
                    var allTypes = assembly.GetTypes();
                    foreach (var type in allTypes)
                    {
                        _logger.LogDebug("Found type in assembly {file}: {TypeName}", file, type.FullName);
                    }

                    var pluginTypes = allTypes
                        .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);

                    foreach (var pluginType in pluginTypes)
                    {
                        _logger.LogDebug("Instantiating plugin type: {PluginType}", pluginType.FullName);
                    }

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
        // For demonstration, we assume the plugin is valid.
        return true;
    }
}