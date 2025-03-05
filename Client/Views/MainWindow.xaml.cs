using System.Windows;
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
}