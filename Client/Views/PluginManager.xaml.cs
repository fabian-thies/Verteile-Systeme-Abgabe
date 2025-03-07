// PluginManager.xaml.cs

using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Client.Views;

public partial class PluginManager : UserControl
{
    private readonly ILogger<PluginManager> _logger;
    private IPlugin[] _loadedPlugins = Array.Empty<IPlugin>();

    public PluginManager()
    {
        InitializeComponent();
        using var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<PluginManager>();
    }

    private void LoadPluginsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pluginDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            var pluginLoaderLogger =
                LoggerFactory.Create(builder => { builder.AddConsole(); }).CreateLogger<PluginLoader>();
            var loader = new PluginLoader(pluginLoaderLogger);
            _loadedPlugins = loader.LoadPlugins(pluginDirectory);
            PluginsListBox.Items.Clear();
            foreach (var plugin in _loadedPlugins) PluginsListBox.Items.Add(plugin.Name);
            StatusTextBlock.Text = _loadedPlugins.Length + " plugin(s) loaded.";
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null) mainWindow.UpdateLoadedPlugins(_loadedPlugins);
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
            var openFileDialog = new OpenFileDialog
            {
                Filter = "DLL files (*.dll)|*.dll",
                Title = "Select Plugin DLL"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                var selectedFile = openFileDialog.FileName;
                var pluginDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                if (!Directory.Exists(pluginDirectory)) Directory.CreateDirectory(pluginDirectory);
                var destFile = Path.Combine(pluginDirectory, Path.GetFileName(selectedFile));
                File.Copy(selectedFile, destFile, true);
                LoadPluginsButton_Click(sender, e);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while loading plugin file.");
            MessageBox.Show("Error loading plugin file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecutePluginButton_Click(object sender, RoutedEventArgs e)
    {
        if (PluginsListBox.SelectedIndex < 0)
        {
            MessageBox.Show("Please select a plugin to execute.", "No Plugin Selected",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var selectedPluginName = PluginsListBox.SelectedItem.ToString();
            var plugin = _loadedPlugins.FirstOrDefault(p =>
                p.Name.Equals(selectedPluginName, StringComparison.OrdinalIgnoreCase));
            if (plugin == null)
            {
                MessageBox.Show("Selected plugin could not be found.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            plugin.Initialize();
            if (!plugin.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase))
            {
                plugin.Execute();
                MessageBox.Show("Plugin '" + plugin.Name + "'.",
                    "Plugin Execution", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Whiteboard plugin initialized. Use the Chat window's button to open it.",
                    "Plugin Execution", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing plugin.");
            MessageBox.Show("Error executing plugin.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}