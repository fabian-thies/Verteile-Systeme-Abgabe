// File: Client/Views/MainWindow.xaml.cs
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
        private readonly HubConnection connection;

        public MainWindow(HubConnection hubConnection)
        {
            InitializeComponent();
            connection = hubConnection;
            RegisterSignalREvents();
        }

        private void RegisterSignalREvents()
        {
            connection.On<string, string>("ReceivePrivateMessage",
                (sender, message) =>
                {
                    // English comment: Add private message to list.
                    Dispatcher.Invoke(new Action(() => { PrivateChatListBox.Items.Add($"{sender}: {message}"); }));
                });

            connection.On<string, string>("ReceiveGroupMessage",
                (sender, message) =>
                {
                    // English comment: Add group message to list.
                    Dispatcher.Invoke(new Action(() => { GroupChatListBox.Items.Add($"{sender}: {message}"); }));
                });

            connection.On<string>("ReceiveSystemMessage", message =>
            {
                // English comment: Display system message.
                Dispatcher.Invoke(new Action(() =>
                {
                    PrivateChatListBox.Items.Add($"[System]: {message}");
                    GroupChatListBox.Items.Add($"[System]: {message}");
                }));
            });

            // New event: Handle whiteboard plugin request.
            connection.On<string>("ReceiveWhiteboardPluginRequest", requester =>
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    var result = MessageBox.Show(
                        $"{requester} invites you to join a whiteboard session and offers you the plugin. Do you want to install it?",
                        "Whiteboard Plugin Request",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Request the plugin file from the plugin owner.
                        connection.InvokeAsync("RequestPluginFile", requester);
                    }
                }));
            });

            // New event: Receive plugin file and load it.
            connection.On<string, string>("ReceivePluginFile", (sender, base64Content) =>
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    try
                    {
                        // Convert Base64 back to bytes.
                        byte[] pluginBytes = Convert.FromBase64String(base64Content);

                        // Save the plugin DLL to a temporary location.
                        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WhiteboardPlugin.dll");
                        System.IO.File.WriteAllBytes(tempPath, pluginBytes);

                        // Load the plugin via PluginLoader.
                        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                        var pluginLogger = loggerFactory.CreateLogger<PluginLoader>();
                        var loader = new PluginLoader(pluginLogger);
                        var plugins = loader.LoadPlugins(System.IO.Path.GetDirectoryName(tempPath));
                        // Find the "Whiteboard" plugin (case-insensitive).
                        var plugin = plugins.FirstOrDefault(p =>
                            p.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase));
                        if (plugin != null)
                        {
                            // Attempt to call the extended Initialize(connection, sender, false) via reflection.
                            var initMethod = plugin.GetType().GetMethod("Initialize", new Type[] { typeof(HubConnection), typeof(string), typeof(bool) });
                            if (initMethod != null)
                            {
                                initMethod.Invoke(plugin, new object[] { connection, sender, false });
                            }
                            plugin.Execute();
                        }
                        else
                        {
                            MessageBox.Show("Whiteboard plugin could not be loaded.", "Plugin Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error processing plugin file: " + ex.Message, "Plugin Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }));
            });

            // New event: Receive request to send the plugin file.
            connection.On<string>("ReceivePluginFileRequest", (targetUser) =>
            {
                Dispatcher.Invoke(async () =>
                {
                    try
                    {
                        // English comment: Read plugin file from disk and send to target user.
                        string pluginPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "WhiteboardPlugin.dll");
                        if (System.IO.File.Exists(pluginPath))
                        {
                            byte[] pluginBytes = System.IO.File.ReadAllBytes(pluginPath);
                            string base64Content = Convert.ToBase64String(pluginBytes);
                            await connection.InvokeAsync("SendPluginFile", targetUser, base64Content);
                        }
                        else
                        {
                            MessageBox.Show("Plugin file not found.", "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error sending plugin file: " + ex.Message);
                    }
                });
            });
        }

        // --- CHANGED: We now load the plugin dynamically and call it via reflection. ---
        private async void OpenPrivateWhiteboardButton_Click(object sender, RoutedEventArgs e)
        {
            var targetUser = PrivateTargetTextBox.Text.Trim();
            if (string.IsNullOrEmpty(targetUser))
            {
                MessageBox.Show("Please enter a target username for the private whiteboard.", "Missing Target",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // English comment: First check if the Whiteboard plugin is actually loaded in the Plugins folder.
            var plugin = GetWhiteboardPlugin();
            if (plugin == null)
            {
                MessageBox.Show("Whiteboard plugin is not loaded. Please load it via the 'Plugins' tab first.", 
                                "Plugin not found",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // English comment: Send a plugin request to the target user (they can accept and load the plugin).
            try
            {
                await connection.InvokeAsync("RequestWhiteboardPlugin", targetUser);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error sending whiteboard plugin request: " + ex.Message);
                return;
            }

            // If we already have the plugin, call "Initialize(connection, targetUser, false)" via reflection, then Execute().
            var initMethod = plugin.GetType().GetMethod("Initialize", new Type[] { typeof(HubConnection), typeof(string), typeof(bool) });
            if (initMethod != null)
            {
                initMethod.Invoke(plugin, new object[] { connection, targetUser, false });
            }
            plugin.Execute();
        }

        private void OpenGroupWhiteboardButton_Click(object sender, RoutedEventArgs e)
        {
            var groupName = GroupNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(groupName))
            {
                MessageBox.Show("Please enter a group name for the group whiteboard.", "Missing Group Name",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // English comment: Check if plugin is loaded.
            var plugin = GetWhiteboardPlugin();
            if (plugin == null)
            {
                MessageBox.Show("Whiteboard plugin is not loaded. Please load it via the 'Plugins' tab first.",
                                "Plugin not found",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // English comment: Initialize the plugin in group mode, then execute.
            var initMethod = plugin.GetType().GetMethod("Initialize", new Type[] { typeof(HubConnection), typeof(string), typeof(bool) });
            if (initMethod != null)
            {
                initMethod.Invoke(plugin, new object[] { connection, groupName, true });
            }
            plugin.Execute();
        }
        // --- End of changed methods. ---

        private async void SendPrivateMessageButton_Click(object sender, RoutedEventArgs e)
        {
            var targetUser = PrivateTargetTextBox.Text.Trim();
            var message = PrivateMessageTextBox.Text.Trim();
            if (string.IsNullOrEmpty(targetUser) || string.IsNullOrEmpty(message))
                return;

            try
            {
                await connection.InvokeAsync("SendPrivateMessage", targetUser, message);
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
                await connection.InvokeAsync("JoinGroup", groupName);
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
                await connection.InvokeAsync("LeaveGroup", groupName);
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
                await connection.InvokeAsync("SendGroupMessage", groupName, message);
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

        // English comment: Helper method to scan the Plugins folder again for the "Whiteboard" plugin.
        //                 If found, return it; otherwise return null.
        private IPlugin? GetWhiteboardPlugin()
        {
            string pluginDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var pluginLogger = loggerFactory.CreateLogger<PluginLoader>();
            var loader = new PluginLoader(pluginLogger);
            var allPlugins = loader.LoadPlugins(pluginDirectory);
            return allPlugins.FirstOrDefault(p => p.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase));
        }
    }
}
