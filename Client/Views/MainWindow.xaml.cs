using System.Windows;
using System.Windows.Input;
using Microsoft.AspNetCore.SignalR.Client;

namespace Client;

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
                Dispatcher.Invoke(() => { PrivateChatListBox.Items.Add($"{sender}: {message}"); });
            });

        connection.On<string, string>("ReceiveGroupMessage",
            (sender, message) => { Dispatcher.Invoke(() => { GroupChatListBox.Items.Add($"{sender}: {message}"); }); });

        connection.On<string>("ReceiveSystemMessage", message =>
        {
            Dispatcher.Invoke(() =>
            {
                PrivateChatListBox.Items.Add($"[System]: {message}");
                GroupChatListBox.Items.Add($"[System]: {message}");
            });
        });

        // New event: Receive a whiteboard plugin request.
        // Register to receive a whiteboard plugin request.
        connection.On<string>("ReceiveWhiteboardPluginRequest", requester =>
        {
            Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    $"{requester} invites you to join a whiteboard session and offers you the plugin. Do you want to install it?",
                    "Whiteboard Plugin Request",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Invoke a method to request the plugin file from the requester.
                    connection.InvokeAsync("RequestPluginFile", requester);
                }
            });
        });

// Register to receive the plugin file (Base64 encoded).
        connection.On<string, string>("ReceivePluginFile", (sender, base64Content) =>
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Convert Base64 content back to bytes.
                    byte[] pluginBytes = Convert.FromBase64String(base64Content);

                    // Save the plugin DLL to a temporary location.
                    string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WhiteboardPlugin.dll");
                    System.IO.File.WriteAllBytes(tempPath, pluginBytes);

                    // Option 1: Load the plugin via PluginLoader.
                    var loader = new PluginLoader( /* pass a logger instance if available */);
                    var plugins = loader.LoadPlugins(System.IO.Path.GetDirectoryName(tempPath));
                    var whiteboardPlugin = plugins.FirstOrDefault(p =>
                        p.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase));
                    if (whiteboardPlugin != null)
                    {
                        // Here, set target as sender (initiator) for private session.
                        whiteboardPlugin.Initialize( /* pass the SignalR connection */ connection, sender, false);
                        whiteboardPlugin.Execute();
                    }
                    else
                    {
                        MessageBox.Show("Whiteboard plugin could not be loaded.", "Plugin Error", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error processing plugin file: " + ex.Message, "Plugin Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            });
        });
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

    // New event handler to open the whiteboard for private chats.
    // New event handler to open the whiteboard for private chats.
    private async void OpenPrivateWhiteboardButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate target username for private whiteboard.
        var targetUser = PrivateTargetTextBox.Text.Trim();
        if (string.IsNullOrEmpty(targetUser))
        {
            MessageBox.Show("Please enter a target username for the private whiteboard.", "Missing Target", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Send a plugin request to the target user.
        try
        {
            await connection.InvokeAsync("RequestWhiteboardPlugin", targetUser);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error sending whiteboard plugin request: " + ex.Message);
            return;
        }

        // Optionally: The initiating user can also load the plugin on his side.
        var whiteboardPlugin = new WhiteboardPlugin();
        whiteboardPlugin.Initialize(connection, targetUser, false);
        whiteboardPlugin.Execute();

        // Additionally, the initiating user sends the plugin file.
        try
        {
            // Read the plugin DLL from a known location.
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
    }


// New event handler to open the whiteboard for group chats.
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