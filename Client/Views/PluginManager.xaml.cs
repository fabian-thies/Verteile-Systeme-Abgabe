// PluginManager.xaml.cs
// Remove or comment out the part that calls plugin.Execute() so that the Whiteboard plugin does not open its window
// when you press "Execute Selected Plugin." This way, you can still test "execution" for other plugins without
// always opening the Whiteboard window.

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Client.Views
{
    public partial class PluginManager : UserControl
    {
        // Logger instance; in a real application, use dependency injection.
        private readonly ILogger<PluginManager> _logger;
        private IPlugin[] _loadedPlugins = Array.Empty<IPlugin>();

        public PluginManager()
        {
            InitializeComponent();

            // Create a simple logger that writes to Console output for demonstration
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            _logger = loggerFactory.CreateLogger<PluginManager>();
        }

        private void LoadPluginsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string pluginDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                _logger.LogInformation("Attempting to load plugins from {directory}", pluginDirectory);

                // Create a logger for our PluginLoader
                var pluginLoaderLogger = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                }).CreateLogger<PluginLoader>();

                // Load plugins
                var loader = new PluginLoader(pluginLoaderLogger);
                _loadedPlugins = loader.LoadPlugins(pluginDirectory);

                // Populate UI
                PluginsListBox.Items.Clear();
                foreach (var plugin in _loadedPlugins)
                {
                    PluginsListBox.Items.Add(plugin.Name);
                }

                StatusTextBlock.Text = $"{_loadedPlugins.Length} plugin(s) loaded.";

                // IMPORTANT: Update MainWindow so it knows which plugins are loaded
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.UpdateLoadedPlugins(_loadedPlugins);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while loading plugins.");
                StatusTextBlock.Text = "Error loading plugins.";
            }
        }

        private void LoadPluginFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open file dialog to select a plugin DLL
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "DLL files (*.dll)|*.dll",
                    Title = "Select Plugin DLL"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string selectedFile = openFileDialog.FileName;
                    _logger.LogInformation("Selected plugin file: {file}", selectedFile);

                    // Define the target "Plugins" directory
                    string pluginDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                    if (!Directory.Exists(pluginDirectory))
                    {
                        Directory.CreateDirectory(pluginDirectory);
                    }

                    // Copy the DLL into our Plugins directory
                    string destFile = Path.Combine(pluginDirectory, Path.GetFileName(selectedFile));
                    File.Copy(selectedFile, destFile, true);
                    _logger.LogInformation("Copied plugin to {dest}", destFile);

                    // Reload the plugins so the new one shows up in our list
                    LoadPluginsButton_Click(sender, e);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while loading plugin file.");
                MessageBox.Show("Error loading plugin file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // If you do NOT want the Whiteboard window to open when clicking "Execute Selected Plugin,"
        // simply do not call plugin.Execute() for that plugin. Below is one approach:
        private void ExecutePluginButton_Click(object sender, RoutedEventArgs e)
        {
            if (PluginsListBox.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a plugin to execute.",
                                "No Plugin Selected",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            try
            {
                var selectedPluginName = PluginsListBox.SelectedItem.ToString();
                var plugin = _loadedPlugins.FirstOrDefault(p =>
                    p.Name.Equals(selectedPluginName, StringComparison.OrdinalIgnoreCase));

                if (plugin == null)
                {
                    MessageBox.Show("Selected plugin could not be found.",
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return;
                }

                // Initialize the plugin. This typically sets up internal data, but won't open a window
                plugin.Initialize();

                // If you do NOT want to open the window for the Whiteboard plugin, skip calling Execute()
                // or add a condition to only run Execute() if it's some other plugin name.
                if (!plugin.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase))
                {
                    plugin.Execute();  // For other plugins, you might still want to test execution here
                    MessageBox.Show($"Plugin '{plugin.Name}' executed (non-Whiteboard).",
                                    "Plugin Execution",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }
                else
                {
                    // For the Whiteboard, just show a short message or do nothing
                    MessageBox.Show("Whiteboard plugin initialized, but not executed here.\n" +
                                    "Use the Chat window's Whiteboard button to open it.",
                                    "Plugin Execution",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing plugin.");
                MessageBox.Show("Error executing plugin.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
