using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;

namespace Client;

public partial class MainWindow : Window
{
    private HubConnection connection;

    public MainWindow()
    {
        InitializeComponent();
        // Initialize the SignalR connection
        connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/chatHub")
            .Build();

        RegisterSignalREvents();
        ConnectToServer();
    }

    // Connect to the SignalR server
    private async void ConnectToServer()
    {
        try
        {
            await connection.StartAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to connect to server: " + ex.Message);
        }
    }

    // Register event handlers for receiving messages
    private void RegisterSignalREvents()
    {
        // Event for receiving private messages
        connection.On<string, string>("ReceivePrivateMessage", (sender, message) =>
        {
            Dispatcher.Invoke(() =>
            {
                PrivateChatListBox.Items.Add($"{sender}: {message}");
            });
        });

        // Event for receiving group messages
        connection.On<string, string>("ReceiveGroupMessage", (sender, message) =>
        {
            Dispatcher.Invoke(() =>
            {
                GroupChatListBox.Items.Add($"{sender}: {message}");
            });
        });
    }

    // Send a private message using the specified target username
    private async void SendPrivateMessageButton_Click(object sender, RoutedEventArgs e)
    {
        var targetUser = PrivateTargetTextBox.Text.Trim();
        var message = PrivateMessageTextBox.Text.Trim();
        if (string.IsNullOrEmpty(targetUser) || string.IsNullOrEmpty(message))
            return;

        try
        {
            await connection.InvokeAsync("SendPrivateMessage", targetUser, message);
            // Optionally, add the message to your own chat box
            PrivateChatListBox.Items.Add($"Me to {targetUser}: {message}");
            PrivateMessageTextBox.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error sending private message: " + ex.Message);
        }
    }

    // Join a group chat
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

    // Leave a group chat
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

    // Send a message to the currently joined group
    private async void SendGroupMessageButton_Click(object sender, RoutedEventArgs e)
    {
        var groupName = GroupNameTextBox.Text.Trim();
        var message = GroupMessageTextBox.Text.Trim();
        if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(message))
            return;

        try
        {
            await connection.InvokeAsync("SendGroupMessage", groupName, message);
            // Optionally, add the message to your own chat box
            GroupChatListBox.Items.Add($"Me in {groupName}: {message}");
            GroupMessageTextBox.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error sending group message: " + ex.Message);
        }
    }
}
