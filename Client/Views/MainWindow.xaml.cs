// MainWindow.xaml.cs

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
        private IPlugin[] _currentlyLoadedPlugins = Array.Empty<IPlugin>();

        public MainWindow(HubConnection hubConnection)
        {
            InitializeComponent();
            _connection = hubConnection;
            RegisterSignalREvents();
        }

        public void UpdateLoadedPlugins(IPlugin[] currentlyLoaded)
        {
            _currentlyLoadedPlugins = currentlyLoaded ?? Array.Empty<IPlugin>();
        }

        private void RegisterSignalREvents()
        {
            _connection.On<string, string>("ReceivePrivateMessage", (sender, message) =>
            {
                Dispatcher.Invoke(() => { PrivateChatListBox.Items.Add(sender + ": " + message); });
            });
            _connection.On<string, string>("ReceiveGroupMessage", (sender, message) =>
            {
                Dispatcher.Invoke(() => { GroupChatListBox.Items.Add(sender + ": " + message); });
            });
            _connection.On<string>("ReceiveSystemMessage", message =>
            {
                Dispatcher.Invoke(() =>
                {
                    PrivateChatListBox.Items.Add("[System]: " + message);
                    GroupChatListBox.Items.Add("[System]: " + message);
                });
            });
            _connection.On<string>("ReceiveWhiteboardPluginRequest", requester =>
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(requester + " invites you to join a whiteboard session.\n"
                        + "They can send you the plugin file. Once you receive it, place it into your 'Plugins' folder "
                        + "and load it manually from the Plugin Manager.",
                        "Whiteboard Plugin Request", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });
            _connection.On<string, string>("ReceivePluginFile", (sender, base64Content) =>
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        byte[] pluginBytes = Convert.FromBase64String(base64Content);
                        string receivedDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReceivedPlugins");
                        if (!Directory.Exists(receivedDir))
                            Directory.CreateDirectory(receivedDir);
                        string pluginFilePath = Path.Combine(receivedDir, "WhiteboardPlugin.dll");
                        File.WriteAllBytes(pluginFilePath, pluginBytes);
                        MessageBox.Show("A Whiteboard plugin file has been received and saved to:\n\n"
                            + pluginFilePath + "\n\nPlease copy this DLL into your 'Plugins' folder "
                            + "and load it via the Plugin Manager if you want to use it.",
                            "Plugin Received", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error saving plugin file: " + ex.Message,
                            "Plugin Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            });
            _connection.On<string>("ReceivePluginFileRequest", (targetUser) =>
            {
                Dispatcher.Invoke(async () =>
                {
                    try
                    {
                        string pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "WhiteboardPlugin.dll");
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

        private async void OpenPrivateWhiteboardButton_Click(object sender, RoutedEventArgs e)
        {
            var targetUser = PrivateTargetTextBox.Text.Trim();
            if (string.IsNullOrEmpty(targetUser))
            {
                MessageBox.Show("Please enter a target username.", "Missing Target",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var plugin = _currentlyLoadedPlugins
                .FirstOrDefault(p => p.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase));
            if (plugin == null)
            {
                MessageBox.Show("Whiteboard plugin is NOT loaded.\n"
                    + "Please go to the 'Plugins' tab, load the plugin manually,\n"
                    + "and then try again.",
                    "Plugin not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                await _connection.InvokeAsync("RequestWhiteboardPlugin", targetUser);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error sending whiteboard plugin request: " + ex.Message);
                return;
            }
            var initMethod = plugin.GetType().GetMethod("Initialize", new[] { typeof(HubConnection), typeof(string), typeof(bool) });
            if (initMethod != null)
            {
                initMethod.Invoke(plugin, new object[] { _connection, targetUser, false });
            }
            plugin.Execute();
        }

        private void OpenGroupWhiteboardButton_Click(object sender, RoutedEventArgs e)
        {
            var groupName = GroupNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(groupName))
            {
                MessageBox.Show("Please enter a group name.", "Missing Group Name",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var plugin = _currentlyLoadedPlugins
                .FirstOrDefault(p => p.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase));
            if (plugin == null)
            {
                MessageBox.Show("Whiteboard plugin is NOT loaded.\n"
                    + "Please go to the 'Plugins' tab, load the plugin manually,\n"
                    + "and then try again.",
                    "Plugin not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var initMethod = plugin.GetType().GetMethod("Initialize", new[] { typeof(HubConnection), typeof(string), typeof(bool) });
            if (initMethod != null)
            {
                initMethod.Invoke(plugin, new object[] { _connection, groupName, true });
            }
            plugin.Execute();
        }

        private async void SendPrivateMessageButton_Click(object sender, RoutedEventArgs e)
        {
            var targetUser = PrivateTargetTextBox.Text.Trim();
            var message = PrivateMessageTextBox.Text.Trim();
            if (string.IsNullOrEmpty(targetUser) || string.IsNullOrEmpty(message)) return;
            try
            {
                await _connection.InvokeAsync("SendPrivateMessage", targetUser, message);
                PrivateChatListBox.Items.Add("Me to " + targetUser + ": " + message);
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
            if (string.IsNullOrEmpty(groupName)) return;
            try
            {
                await _connection.InvokeAsync("JoinGroup", groupName);
                GroupChatListBox.Items.Add("Joined group " + groupName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error joining group: " + ex.Message);
            }
        }

        private async void LeaveGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var groupName = GroupNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(groupName)) return;
            try
            {
                await _connection.InvokeAsync("LeaveGroup", groupName);
                GroupChatListBox.Items.Add("Left group " + groupName);
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
            if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(message)) return;
            try
            {
                await _connection.InvokeAsync("SendGroupMessage", groupName, message);
                GroupChatListBox.Items.Add("Me in " + groupName + ": " + message);
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
        
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
