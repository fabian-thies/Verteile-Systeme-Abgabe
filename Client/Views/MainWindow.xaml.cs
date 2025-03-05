using System;
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
                    // Explicitly create an Action to remove ambiguity
                    Dispatcher.Invoke(new Action(() => { PrivateChatListBox.Items.Add($"{sender}: {message}"); }));
                });

            connection.On<string, string>("ReceiveGroupMessage",
                (sender, message) =>
                {
                    Dispatcher.Invoke(new Action(() => { GroupChatListBox.Items.Add($"{sender}: {message}"); }));
                });

            connection.On<string>("ReceiveSystemMessage", message =>
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    PrivateChatListBox.Items.Add($"[System]: {message}");
                    GroupChatListBox.Items.Add($"[System]: {message}");
                }));
            });

            // Neues Event: Empfang einer Whiteboard-Plugin-Anfrage.
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
                        // Hier rufen wir den Hub-Aufruf zum Anfordern der Plugin-Datei auf.
                        connection.InvokeAsync("RequestPluginFile", requester);
                    }
                }));
            });

            // Neues Event: Empfang der Plugin-Datei (Base64-kodiert).
            connection.On<string, string>("ReceivePluginFile", (sender, base64Content) =>
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    try
                    {
                        // Base64 zurück in Bytes konvertieren.
                        byte[] pluginBytes = Convert.FromBase64String(base64Content);

                        // Plugin-DLL an einem temporären Ort speichern.
                        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WhiteboardPlugin.dll");
                        System.IO.File.WriteAllBytes(tempPath, pluginBytes);

                        // Plugin via PluginLoader laden.
                        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                        var pluginLogger = loggerFactory.CreateLogger<PluginLoader>();
                        var loader = new PluginLoader(pluginLogger);
                        var plugins = loader.LoadPlugins(System.IO.Path.GetDirectoryName(tempPath));
                        // Suche nach dem Plugin "Whiteboard" (Groß-/Kleinschreibung ignorieren)
                        var plugin = plugins.FirstOrDefault(p =>
                            p.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase));
                        if (plugin != null)
                        {
                            // Da Initialize in IPlugin parameterlos ist, casten wir in den konkreten Typ.
                            ((WhiteboardPlugin)plugin).Initialize(connection, sender, false);
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
        }

        private async void OpenPrivateWhiteboardButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate target username for private whiteboard.
            var targetUser = PrivateTargetTextBox.Text.Trim();
            if (string.IsNullOrEmpty(targetUser))
            {
                MessageBox.Show("Please enter a target username for the private whiteboard.", "Missing Target",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Sende eine Plugin-Anfrage an den Ziel-User.
            try
            {
                await connection.InvokeAsync("RequestWhiteboardPlugin", targetUser);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error sending whiteboard plugin request: " + ex.Message);
                return;
            }

            // Der initiierende User lädt das Plugin auf seiner Seite.
            var whiteboardPlugin = new WhiteboardPlugin();
            // Da Initialize mit Parametern hier nicht über das Interface aufrufbar ist,
            // casten wir in den konkreten Typ.
            ((WhiteboardPlugin)whiteboardPlugin).Initialize(connection, targetUser, false);
            whiteboardPlugin.Execute();

            // Zusätzlich: Sende die Plugin-Datei an den Ziel-User.
            try
            {
                // Plugin-DLL aus einem bekannten Verzeichnis lesen.
                string pluginPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins",
                    "WhiteboardPlugin.dll");
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
        }

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

        private void OpenGroupWhiteboardButton_Click(object sender, RoutedEventArgs e)
        {
            // English comment: Validate group name for group whiteboard.
            var groupName = GroupNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(groupName))
            {
                MessageBox.Show("Please enter a group name for the group whiteboard.", "Missing Group Name",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // English comment: Create and initialize the whiteboard plugin in group mode.
            var whiteboardPlugin = new WhiteboardPlugin();
            whiteboardPlugin.Initialize(connection, groupName, true);
            whiteboardPlugin.Execute();
        }
    }
}