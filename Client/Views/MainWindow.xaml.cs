using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Client
{
    public partial class MainWindow : Window
    {
        private readonly HubConnection _connection;

        // We track which plugins have been manually loaded so that
        // we can check at "Whiteboard" button clicks whether the plugin is ready.
        private IPlugin[] _currentlyLoadedPlugins = Array.Empty<IPlugin>();

        public MainWindow(HubConnection hubConnection)
        {
            InitializeComponent();
            _connection = hubConnection;
            RegisterSignalREvents();
        }

        private void RegisterSignalREvents()
        {
            _connection.On<string, string>("ReceivePrivateMessage",
                (sender, message) =>
                {
                    Dispatcher.Invoke(() => { PrivateChatListBox.Items.Add($"{sender}: {message}"); });
                });

            _connection.On<string, string>("ReceiveGroupMessage",
                (sender, message) =>
                {
                    Dispatcher.Invoke(() => { GroupChatListBox.Items.Add($"{sender}: {message}"); });
                });

            _connection.On<string>("ReceiveSystemMessage", message =>
            {
                Dispatcher.Invoke(() =>
                {
                    PrivateChatListBox.Items.Add($"[System]: {message}");
                    GroupChatListBox.Items.Add($"[System]: {message}");
                });
            });

            // Removed automatic loading after plugin request
            _connection.On<string>("ReceiveWhiteboardPluginRequest", requester =>
            {
                // English comment: Show a hint only, do nothing automatically
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"{requester} invites you to join a whiteboard session.\n" +
                        "They can send you the plugin file. Once you receive it, place it into your 'Plugins' folder and load it manually from the Plugin Manager.",
                        "Whiteboard Plugin Request",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
            });

            // Removed automatic instantiation in `ReceivePluginFile`
            _connection.On<string, string>("ReceivePluginFile", (sender, base64Content) =>
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // English comment: We save the DLL locally but do not load it automatically
                        byte[] pluginBytes = Convert.FromBase64String(base64Content);

                        string receivedDir = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "ReceivedPlugins");

                        if (!Directory.Exists(receivedDir))
                            Directory.CreateDirectory(receivedDir);

                        string pluginFilePath =
                            Path.Combine(receivedDir, "WhiteboardPlugin.dll");
                        File.WriteAllBytes(pluginFilePath, pluginBytes);

                        MessageBox.Show(
                            "A Whiteboard plugin file has been received and saved to:\n\n"
                            + pluginFilePath
                            + "\n\nPlease copy this DLL into your 'Plugins' folder and load it via the Plugin Manager if you want to use it.",
                            "Plugin Received",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error saving plugin file: " + ex.Message,
                            "Plugin Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            });

            // Still possible: "ReceivePluginFileRequest"
            _connection.On<string>("ReceivePluginFileRequest", (targetUser) =>
            {
                Dispatcher.Invoke(async () =>
                {
                    try
                    {
                        // English comment: We are sending the plugin file if we have it locally
                        string pluginPath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "Plugins",
                            "WhiteboardPlugin.dll"
                        );

                        if (File.Exists(pluginPath))
                        {
                            byte[] pluginBytes = File.ReadAllBytes(pluginPath);
                            string base64Content = Convert.ToBase64String(pluginBytes);
                            await _connection.InvokeAsync("SendPluginFile", targetUser, base64Content);
                        }
                        else
                        {
                            MessageBox.Show("Plugin file not found.", "File Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error sending plugin file: " + ex.Message);
                    }
                });
            });
        }

        // ---------------------------------------------------
        // Private Whiteboard
        // ---------------------------------------------------
        private async void OpenPrivateWhiteboardButton_Click(object sender, RoutedEventArgs e)
        {
            var targetUser = PrivateTargetTextBox.Text.Trim();
            if (string.IsNullOrEmpty(targetUser))
            {
                MessageBox.Show("Please enter a target username for the private whiteboard.",
                                "Missing Target",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // English comment: Check if the Whiteboard plugin is already loaded in the Plugin Manager
            var plugin = _currentlyLoadedPlugins
                .FirstOrDefault(p => p.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase));

            if (plugin == null)
            {
                MessageBox.Show("Whiteboard plugin is NOT loaded.\n" +
                                "Please go to the 'Plugins' tab, load the plugin manually,\n" +
                                "and then try again.",
                                "Plugin not found",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            // Optional: invite the other user
            try
            {
                await _connection.InvokeAsync("RequestWhiteboardPlugin", targetUser);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error sending whiteboard plugin request: " + ex.Message);
                return;
            }

            // Because there's no reference to WhiteboardPlugin, we use reflection for the custom Initialize method
            // Then we can call plugin.Execute() which is part of the IPlugin interface.
            var initMethod = plugin.GetType().GetMethod(
                "Initialize",
                new[] { typeof(HubConnection), typeof(string), typeof(bool) }
            );
            if (initMethod != null)
            {
                initMethod.Invoke(plugin, new object[] { _connection, targetUser, false });
            }

            plugin.Execute();
        }

        // ---------------------------------------------------
        // Group Whiteboard
        // ---------------------------------------------------
        private void OpenGroupWhiteboardButton_Click(object sender, RoutedEventArgs e)
        {
            var groupName = GroupNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(groupName))
            {
                MessageBox.Show("Please enter a group name for the group whiteboard.",
                                "Missing Group Name",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            var plugin = _currentlyLoadedPlugins
                .FirstOrDefault(p => p.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase));

            if (plugin == null)
            {
                MessageBox.Show("Whiteboard plugin is NOT loaded.\n" +
                                "Please go to the 'Plugins' tab, load the plugin manually,\n" +
                                "and then try again.",
                                "Plugin not found",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            // Reflection to call Initialize(_connection, groupName, true)
            var initMethod = plugin.GetType().GetMethod(
                "Initialize",
                new[] { typeof(HubConnection), typeof(string), typeof(bool) }
            );
            if (initMethod != null)
            {
                initMethod.Invoke(plugin, new object[] { _connection, groupName, true });
            }

            plugin.Execute();
        }

        // ---------------------------------------------------
        // Example method: the PluginManager calls this to update
        // the list of manually loaded plugins.
        // ---------------------------------------------------
        public void UpdateLoadedPlugins(IPlugin[] currentlyLoaded)
        {
            _currentlyLoadedPlugins = currentlyLoaded;
        }

        // ---------------------------------------------------
        // Remaining chat methods unchanged
        // ---------------------------------------------------
        private async void SendPrivateMessageButton_Click(object sender, RoutedEventArgs e)
        {
            var targetUser = PrivateTargetTextBox.Text.Trim();
            var message = PrivateMessageTextBox.Text.Trim();
            if (string.IsNullOrEmpty(targetUser) || string.IsNullOrEmpty(message))
                return;

            try
            {
                await _connection.InvokeAsync("SendPrivateMessage", targetUser, message);
                PrivateChatListBox.Items.Add($"Me to {targetUser}: {message}");
                PrivateMessageTextBox.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error sending private message: " + ex.Message);
            }
        }

        private async void JoinGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var groupName = GroupNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(groupName))
                return;

            try
            {
                await _connection.InvokeAsync("JoinGroup", groupName);
                GroupChatListBox.Items.Add($"Joined group {groupName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error joining group: " + ex.Message);
            }
        }

        private async void LeaveGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var groupName = GroupNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(groupName))
                return;

            try
            {
                await _connection.InvokeAsync("LeaveGroup", groupName);
                GroupChatListBox.Items.Add($"Left group {groupName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error leaving group: " + ex.Message);
            }
        }

        private async void SendGroupMessageButton_Click(object sender, RoutedEventArgs e)
        {
            var groupName = GroupNameTextBox.Text.Trim();
            var message = GroupMessageTextBox.Text.Trim();
            if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(message))
                return;

            try
            {
                await _connection.InvokeAsync("SendGroupMessage", groupName, message);
                GroupChatListBox.Items.Add($"Me in {groupName}: {message}");
                GroupMessageTextBox.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error sending group message: " + ex.Message);
            }
        }

        private void PrivateMessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SendPrivateMessageButton_Click(sender, e);
            }
        }

        private void GroupMessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SendGroupMessageButton_Click(sender, e);
            }
        }
    }
}
