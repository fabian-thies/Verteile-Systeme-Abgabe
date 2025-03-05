// PluginManager.xaml.cs
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
        private IPlugin[] _loadedPlugins;

        public PluginManager()
        {
            InitializeComponent();
            // For demonstration, we create a simple logger that writes to Debug output.
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            _logger = loggerFactory.CreateLogger<PluginManager>();
        }

        // Click event to load plugins from the Plugins folder.
        private void LoadPluginsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string pluginDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                _logger.LogInformation("Attempting to load plugins from {directory}", pluginDirectory);
        
                // Create a specific logger for PluginLoader
                var pluginLoaderLogger = LoggerFactory.Create(builder => 
                {
                    builder.AddConsole();
                }).CreateLogger<PluginLoader>();
        
                var loader = new PluginLoader(pluginLoaderLogger);
                _loadedPlugins = loader.LoadPlugins(pluginDirectory);

                PluginsListBox.Items.Clear();
                foreach (var plugin in _loadedPlugins)
                {
                    PluginsListBox.Items.Add(plugin.Name);
                }

                StatusTextBlock.Text = $"{_loadedPlugins.Length} plugin(s) loaded.";
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
                // Open file dialog to select a plugin DLL.
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "DLL files (*.dll)|*.dll",
                    Title = "Select Plugin DLL"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    string selectedFile = openFileDialog.FileName;
                    _logger.LogInformation("Selected plugin file: {file}", selectedFile);
                    // Define the target directory (Plugins folder).
                    string pluginDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                    // Create the Plugins directory if it doesn't exist.
                    if (!Directory.Exists(pluginDirectory))
                    {
                        Directory.CreateDirectory(pluginDirectory);
                    }
                    // Copy the file to the Plugins directory.
                    string destFile = Path.Combine(pluginDirectory, Path.GetFileName(selectedFile));
                    File.Copy(selectedFile, destFile, true);
                    _logger.LogInformation("Copied plugin to {dest}", destFile);
                    // Reload plugins after copying.
                    LoadPluginsButton_Click(sender, e);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while loading plugin file.");
                MessageBox.Show("Error loading plugin file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Click event to execute the selected plugin.
        private void ExecutePluginButton_Click(object sender, RoutedEventArgs e)
        {
            if (PluginsListBox.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a plugin to execute.", "No Plugin Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var selectedPluginName = PluginsListBox.SelectedItem.ToString();
                var plugin = _loadedPlugins.FirstOrDefault(p => p.Name.Equals(selectedPluginName, StringComparison.OrdinalIgnoreCase));
                if (plugin != null)
                {
                    // Initialize and execute the plugin.
                    plugin.Initialize();
                    plugin.Execute();
                    MessageBox.Show($"Plugin '{plugin.Name}' executed.", "Plugin Execution", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Selected plugin could not be found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
