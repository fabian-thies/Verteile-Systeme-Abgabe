using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Win32;
using Shared.Models;
using Microsoft.Extensions.Logging;

namespace Client;

public partial class MainWindow : Window
{
    private readonly HubConnection _connection;
    private IPlugin[] _currentlyLoadedPlugins = Array.Empty<IPlugin>();
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(HubConnection hubConnection)
    {
        InitializeComponent();
        _connection = hubConnection;

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<MainWindow>();
        _logger.LogInformation("MainWindow initialized and SignalR connection set.");

        RegisterSignalREvents();
        LoadOpenGroups();
    }

    public void UpdateLoadedPlugins(IPlugin[] currentlyLoaded)
    {
        _currentlyLoadedPlugins = currentlyLoaded ?? Array.Empty<IPlugin>();
        _logger.LogInformation("Loaded plugins updated. Total plugins loaded: {Count}", _currentlyLoadedPlugins.Length);
    }

    public bool IsPluginLoaded(string pluginName)
    {
        bool isLoaded = _currentlyLoadedPlugins.Any(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));
        _logger.LogInformation("Plugin '{PluginName}' loaded status: {IsLoaded}", pluginName, isLoaded);
        return isLoaded;
    }

    private void RegisterSignalREvents()
    {
        _logger.LogInformation("Registering SignalR event handlers.");

        _connection.On<string, string>("ReceivePrivateMessage",
            (sender, message) =>
            {
                _logger.LogInformation("Received private message from {Sender}: {Message}", sender, message);
                Dispatcher.Invoke(() => { PrivateChatListBox.Items.Add(sender + ": " + message); });
            });

        _connection.On<string, string>("ReceiveGroupMessage",
            (sender, message) =>
            {
                _logger.LogInformation("Received group message from {Sender}: {Message}", sender, message);
                Dispatcher.Invoke(() => { GroupChatListBox.Items.Add(sender + ": " + message); });
            });

        _connection.On<string>("ReceiveSystemMessage", message =>
        {
            _logger.LogInformation("Received system message: {Message}", message);
            Dispatcher.Invoke(() =>
            {
                PrivateChatListBox.Items.Add("[System]: " + message);
                GroupChatListBox.Items.Add("[System]: " + message);
            });
        });

        _connection.On<List<string>>("ReceiveGroupList", groupList =>
        {
            _logger.LogInformation("Received group list update with {Count} groups.", groupList.Count);
            Dispatcher.Invoke(() =>
            {
                OpenGroupsListBox.Items.Clear();
                if (groupList.Count == 0)
                {
                    OpenGroupsListBox.Items.Add("No open groups");
                }
                else
                {
                    foreach (var group in groupList)
                    {
                        OpenGroupsListBox.Items.Add(group);
                    }
                }
            });
        });

        _connection.On<string>("ReceiveWhiteboardPluginRequest", async requester =>
        {
            _logger.LogInformation("Received whiteboard plugin request from {Requester}", requester);
            await Dispatcher.InvokeAsync(async () =>
            {
                if (IsPluginLoaded("Whiteboard"))
                {
                    _logger.LogInformation("Whiteboard plugin already loaded. No action taken.");
                    return;
                }

                var result = MessageBox.Show(
                    requester +
                    " invites you to join a whiteboard session.\nDo you want to automatically load the plugin?",
                    "Whiteboard Plugin Request", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _logger.LogInformation("User accepted whiteboard plugin request from {Requester}", requester);
                    await _connection.InvokeAsync("RequestPluginFile", requester);
                }
                else
                {
                    _logger.LogInformation("User declined whiteboard plugin request from {Requester}", requester);
                }
            });
        });

        _connection.On<string, string>("ReceivePluginFile", (sender, base64Content) =>
        {
            _logger.LogInformation("Received plugin file from {Sender}", sender);
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var pluginBytes = Convert.FromBase64String(base64Content);
                    var pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                    if (!Directory.Exists(pluginDir))
                    {
                        Directory.CreateDirectory(pluginDir);
                        _logger.LogInformation("Plugin directory created at {PluginDir}", pluginDir);
                    }

                    var pluginFilePath = Path.Combine(pluginDir, "WhiteboardPlugin.dll");
                    File.WriteAllBytes(pluginFilePath, pluginBytes);
                    _logger.LogInformation("Plugin file saved at {PluginFilePath}", pluginFilePath);
                    MessageBox.Show("Whiteboard plugin has been automatically loaded.", "Plugin Loaded",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving plugin file.");
                    MessageBox.Show("Error saving plugin file: " + ex.Message, "Plugin Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            });
        });

        _connection.On<string>("ReceivePluginFileRequest", async targetUser =>
        {
            _logger.LogInformation("Received plugin file request for user {TargetUser}", targetUser);
            await Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins",
                        "WhiteboardPlugin.dll");
                    if (File.Exists(pluginPath))
                    {
                        var pluginBytes = File.ReadAllBytes(pluginPath);
                        var base64Content = Convert.ToBase64String(pluginBytes);
                        _logger.LogInformation("Sending plugin file to {TargetUser}", targetUser);
                        await _connection.InvokeAsync("SendPluginFile", targetUser, base64Content);
                    }
                    else
                    {
                        _logger.LogWarning("Plugin file not found at {PluginPath}", pluginPath);
                        MessageBox.Show("Plugin file not found.", "File Error", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending plugin file.");
                    MessageBox.Show("Error sending plugin file: " + ex.Message);
                }
            });
        });
    }

    private async void LoadOpenGroups()
    {
        try
        {
            var groups = await _connection.InvokeAsync<List<string>>("GetOpenGroups");
            Dispatcher.Invoke(() =>
            {
                OpenGroupsListBox.Items.Clear();
                if (groups.Count == 0)
                {
                    OpenGroupsListBox.Items.Add("No open groups");
                }
                else
                {
                    foreach (var group in groups)
                    {
                        OpenGroupsListBox.Items.Add(group);
                    }
                }
            });
            _logger.LogInformation("Loaded open groups on startup.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading open groups on startup.");
        }
    }
    
    private void OpenGroupsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (OpenGroupsListBox.SelectedItem is string selectedGroup && selectedGroup != "No open groups")
        {
            GroupNameTextBox.Text = selectedGroup;
        }
    }
    
    private async void UploadFileButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("UploadFileButton clicked.");
        var openFileDialog = new OpenFileDialog
        {
            Filter = "All Files (*.*)|*.*",
            Title = "Select a file to upload"
        };
        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var filePath = openFileDialog.FileName;
                _logger.LogInformation("Selected file for upload: {FilePath}", filePath);
                var fileBytes = await File.ReadAllBytesAsync(filePath);
                var base64Content = Convert.ToBase64String(fileBytes);
                var filename = Path.GetFileName(filePath);
                var metadata = "{}";
                var author = "MyUser";
                var documentId =
                    await _connection.InvokeAsync<int>("UploadDocument", filename, base64Content, author, metadata);
                UploadStatusTextBlock.Text = "File uploaded successfully. Document ID: " + documentId;
                _logger.LogInformation("File uploaded successfully with Document ID: {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file.");
                UploadStatusTextBlock.Text = "Error uploading file: " + ex.Message;
            }
        }
    }

    private async void DownloadFileButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("DownloadFileButton clicked.");
        if (int.TryParse(DocumentIdTextBox.Text, out var documentId))
        {
            try
            {
                _logger.LogInformation("Attempting to download document with ID: {DocumentId}", documentId);
                var jsonResponse = await _connection.InvokeAsync<string>("DownloadDocument", documentId);
                if (jsonResponse == null)
                {
                    DownloadStatusTextBlock.Text = "Document not found.";
                    _logger.LogWarning("Document with ID {DocumentId} not found.", documentId);
                    return;
                }

                var fileDownloadInfo = JsonSerializer.Deserialize<FileDownloadInfo>(jsonResponse);
                var saveFileDialog = new SaveFileDialog
                {
                    FileName = fileDownloadInfo.FileName,
                    Filter = "All Files (*.*)|*.*",
                    Title = "Save downloaded file"
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    var fileBytes = Convert.FromBase64String(fileDownloadInfo.Base64Content);
                    await File.WriteAllBytesAsync(saveFileDialog.FileName, fileBytes);
                    DownloadStatusTextBlock.Text = "File downloaded successfully.";
                    _logger.LogInformation("File downloaded and saved to {SaveFilePath}", saveFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file with ID: {DocumentId}", documentId);
                DownloadStatusTextBlock.Text = "Error downloading file: " + ex.Message;
            }
        }
        else
        {
            DownloadStatusTextBlock.Text = "Invalid document ID.";
            _logger.LogWarning("Invalid document ID entered: {DocumentIdText}", DocumentIdTextBox.Text);
        }
    }

    private async void LoadVersionsButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("LoadVersionsButton clicked.");
        if (!int.TryParse(FileIdForVersionTextBox.Text.Trim(), out var fileId))
        {
            _logger.LogWarning("Invalid File ID entered for version loading: {FileIdText}",
                FileIdForVersionTextBox.Text);
            MessageBox.Show("Please enter a valid File ID.", "Invalid File ID", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            _logger.LogInformation("Loading document versions for File ID: {FileId}", fileId);
            var versions = await _connection.InvokeAsync<List<DocumentVersion>>("GetDocumentVersionsById", fileId);
            FileVersionsListBox.Items.Clear();
            if (versions != null && versions.Any())
            {
                foreach (var doc in versions)
                    FileVersionsListBox.Items.Add("FileID: " + doc.Id + ", Version: " + doc.Version + ", Uploaded: " +
                                                  doc.UploadTimestamp);
                _logger.LogInformation("Loaded {Count} versions for File ID: {FileId}", versions.Count, fileId);
            }
            else
            {
                FileVersionsListBox.Items.Add("No versions found.");
                _logger.LogInformation("No versions found for File ID: {FileId}", fileId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading versions for File ID: {FileId}", fileId);
            MessageBox.Show("Error loading versions: " + ex.Message);
        }
    }

    private async void LoadAllFilesButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("LoadAllFilesButton clicked.");
        try
        {
            var allFiles = await _connection.InvokeAsync<List<DocumentVersion>>("GetAllDocuments");
            AllFilesListBox.Items.Clear();
            if (allFiles != null && allFiles.Any())
            {
                foreach (var doc in allFiles)
                    AllFilesListBox.Items.Add("FileID: " + doc.Id + ", Name: " + doc.Filename + ", Version: " +
                                              doc.Version + ", Author: " + doc.Author + ", Uploaded: " +
                                              doc.UploadTimestamp);
                _logger.LogInformation("Loaded all files. Total files: {Count}", allFiles.Count);
            }
            else
            {
                AllFilesListBox.Items.Add("No files found.");
                _logger.LogInformation("No files found.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading all files.");
            MessageBox.Show("Error loading all files: " + ex.Message);
        }
    }

    private async void SendPrivateMessageButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("SendPrivateMessageButton clicked.");
        var targetUser = PrivateTargetTextBox.Text.Trim();
        var message = PrivateMessageTextBox.Text.Trim();
        if (string.IsNullOrEmpty(targetUser) || string.IsNullOrEmpty(message))
        {
            _logger.LogWarning("Private message not sent. Target user or message is empty.");
            return;
        }

        // Process the message through plugins (e.g., moderation plugin).
        var processedMessage = ProcessMessageThroughPlugins(message);

        try
        {
            await _connection.InvokeAsync("SendPrivateMessage", targetUser, processedMessage);
            PrivateChatListBox.Items.Add("Me to " + targetUser + ": " + processedMessage);
            _logger.LogInformation("Private message sent to {TargetUser}: {Message}", targetUser, processedMessage);
            PrivateMessageTextBox.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending private message to {TargetUser}", targetUser);
            MessageBox.Show("Error sending private message: " + ex.Message);
        }
    }

    private async void JoinGroupButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("JoinGroupButton clicked.");
        var groupName = GroupNameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(groupName))
        {
            _logger.LogWarning("Join group failed. Group name is empty.");
            return;
        }

        try
        {
            await _connection.InvokeAsync("JoinGroup", groupName);
            GroupChatListBox.Items.Add("Joined group " + groupName);
            _logger.LogInformation("Joined group: {GroupName}", groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining group: {GroupName}", groupName);
            MessageBox.Show("Error joining group: " + ex.Message);
        }
    }

    private async void LeaveGroupButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("LeaveGroupButton clicked.");
        var groupName = GroupNameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(groupName))
        {
            _logger.LogWarning("Leave group failed. Group name is empty.");
            return;
        }

        try
        {
            await _connection.InvokeAsync("LeaveGroup", groupName);
            GroupChatListBox.Items.Add("Left group " + groupName);
            _logger.LogInformation("Left group: {GroupName}", groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving group: {GroupName}", groupName);
            MessageBox.Show("Error leaving group: " + ex.Message);
        }
    }

    private async void SendGroupMessageButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("SendGroupMessageButton clicked.");
        var groupName = GroupNameTextBox.Text.Trim();
        var message = GroupMessageTextBox.Text.Trim();
        if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(message))
        {
            _logger.LogWarning("Group message not sent. Group name or message is empty.");
            return;
        }

        var processedMessage = ProcessMessageThroughPlugins(message);

        try
        {
            await _connection.InvokeAsync("SendGroupMessage", groupName, processedMessage);
            GroupChatListBox.Items.Add("Me in " + groupName + ": " + processedMessage);
            _logger.LogInformation("Group message sent to {GroupName}: {Message}", groupName, processedMessage);
            GroupMessageTextBox.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending group message to {GroupName}", groupName);
            MessageBox.Show("Error sending group message: " + ex.Message);
        }
    }

    private void PrivateMessageTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _logger.LogInformation(
                "Enter key pressed in PrivateMessageTextBox. Triggering SendPrivateMessageButton_Click.");
            SendPrivateMessageButton_Click(sender, e);
        }
    }

    private void GroupMessageTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _logger.LogInformation(
                "Enter key pressed in GroupMessageTextBox. Triggering SendGroupMessageButton_Click.");
            SendGroupMessageButton_Click(sender, e);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _logger.LogInformation("Title bar clicked for dragging window.");
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Close button clicked. Closing MainWindow.");
        Close();
    }

    private async void OpenPrivateWhiteboardButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("OpenPrivateWhiteboardButton clicked.");
        var targetUser = PrivateTargetTextBox.Text.Trim();
        if (string.IsNullOrEmpty(targetUser))
        {
            _logger.LogWarning("Target username is empty for private whiteboard.");
            MessageBox.Show("Please enter a target username.", "Missing Target", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var plugin =
            _currentlyLoadedPlugins.FirstOrDefault(p =>
                p.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase));
        if (plugin == null)
        {
            _logger.LogWarning("Whiteboard plugin not loaded for private session.");
            MessageBox.Show(
                "Whiteboard plugin is NOT loaded.\nPlease go to the 'Plugins' tab, load the plugin manually, and then try again.",
                "Plugin not found", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _logger.LogInformation("Sending whiteboard plugin request for private session to {TargetUser}", targetUser);
            await _connection.InvokeAsync("RequestWhiteboardPlugin", targetUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending whiteboard plugin request for private session to {TargetUser}",
                targetUser);
            MessageBox.Show("Error sending whiteboard plugin request: " + ex.Message);
            return;
        }

        var initMethod = plugin.GetType()
            .GetMethod("Initialize", new[] { typeof(HubConnection), typeof(string), typeof(bool) });
        if (initMethod != null)
        {
            _logger.LogInformation("Initializing whiteboard plugin for private session with target user {TargetUser}",
                targetUser);
            initMethod.Invoke(plugin, new object[] { _connection, targetUser, false });
        }
        else
        {
            _logger.LogWarning("Initialization method not found in whiteboard plugin for private session.");
        }

        plugin.Execute();
        _logger.LogInformation("Whiteboard plugin executed for private session.");
    }

    private void OpenGroupWhiteboardButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("OpenGroupWhiteboardButton clicked.");
        var groupName = GroupNameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(groupName))
        {
            _logger.LogWarning("Group name is empty for group whiteboard.");
            MessageBox.Show("Please enter a group name.", "Missing Group Name", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var plugin =
            _currentlyLoadedPlugins.FirstOrDefault(p =>
                p.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase));
        if (plugin == null)
        {
            _logger.LogWarning("Whiteboard plugin not loaded for group session.");
            MessageBox.Show(
                "Whiteboard plugin is NOT loaded.\nPlease go to the 'Plugins' tab, load the plugin manually, and then try again.",
                "Plugin not found", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var initMethod = plugin.GetType()
            .GetMethod("Initialize", new[] { typeof(HubConnection), typeof(string), typeof(bool) });
        if (initMethod != null)
        {
            _logger.LogInformation("Initializing whiteboard plugin for group session with group {GroupName}",
                groupName);
            initMethod.Invoke(plugin, new object[] { _connection, groupName, true });
        }
        else
        {
            _logger.LogWarning("Initialization method not found in whiteboard plugin for group session.");
        }

        plugin.Execute();
        _logger.LogInformation("Whiteboard plugin executed for group session.");
    }

    private string ProcessMessageThroughPlugins(string message)
    {
        foreach (var plugin in _currentlyLoadedPlugins)
        {
            var processMethod = plugin.GetType().GetMethod("ProcessMessage", new Type[] { typeof(string) });
            if (processMethod != null)
            {
                message = (string)processMethod.Invoke(plugin, new object[] { message });
            }
        }

        return message;
    }
}