using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Win32;

namespace Client
{
    public class DocumentVersion
    {
        public int Id { get; set; }
        public string Filename { get; set; }
        public string Author { get; set; }
        public int Version { get; set; }
        public DateTime UploadTimestamp { get; set; }
    }

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

        public bool IsPluginLoaded(string pluginName)
        {
            return _currentlyLoadedPlugins.Any(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));
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
            _connection.On<string>("ReceiveWhiteboardPluginRequest", async requester =>
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    if (IsPluginLoaded("Whiteboard"))
                        return;
                    var result = MessageBox.Show(requester + " invites you to join a whiteboard session.\nDo you want to automatically load the plugin?", "Whiteboard Plugin Request", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        await _connection.InvokeAsync("RequestPluginFile", requester);
                    }
                });
            });
            _connection.On<string, string>("ReceivePluginFile", (sender, base64Content) =>
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        byte[] pluginBytes = Convert.FromBase64String(base64Content);
                        string pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                        if (!Directory.Exists(pluginDir))
                            Directory.CreateDirectory(pluginDir);
                        string pluginFilePath = Path.Combine(pluginDir, "WhiteboardPlugin.dll");
                        File.WriteAllBytes(pluginFilePath, pluginBytes);
                        MessageBox.Show("Whiteboard plugin has been automatically loaded.", "Plugin Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error saving plugin file: " + ex.Message, "Plugin Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            });
            _connection.On<string>("ReceivePluginFileRequest", async (targetUser) =>
            {
                await Dispatcher.InvokeAsync(async () =>
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

        private async void UploadFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "All Files (*.*)|*.*",
                Title = "Select a file to upload"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string filePath = openFileDialog.FileName;
                    byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                    string base64Content = Convert.ToBase64String(fileBytes);
                    string filename = Path.GetFileName(filePath);
                    string metadata = "{}";
                    int documentId = await _connection.InvokeAsync<int>("UploadDocument", filename, base64Content, metadata);
                    UploadStatusTextBlock.Text = $"File uploaded successfully. Document ID: {documentId}";
                }
                catch (Exception ex)
                {
                    UploadStatusTextBlock.Text = "Error uploading file: " + ex.Message;
                }
            }
        }

        private async void DownloadFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(DocumentIdTextBox.Text, out int documentId))
            {
                try
                {
                    string base64Content = await _connection.InvokeAsync<string>("DownloadDocument", documentId);
                    if (base64Content == null)
                    {
                        DownloadStatusTextBlock.Text = "Document not found.";
                        return;
                    }
                    var saveFileDialog = new SaveFileDialog
                    {
                        FileName = "downloaded_file",
                        Filter = "All Files (*.*)|*.*",
                        Title = "Save downloaded file"
                    };
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        byte[] fileBytes = Convert.FromBase64String(base64Content);
                        await File.WriteAllBytesAsync(saveFileDialog.FileName, fileBytes);
                        DownloadStatusTextBlock.Text = "File downloaded successfully.";
                    }
                }
                catch (Exception ex)
                {
                    DownloadStatusTextBlock.Text = "Error downloading file: " + ex.Message;
                }
            }
            else
            {
                DownloadStatusTextBlock.Text = "Invalid document ID.";
            }
        }

        private async void LoadVersionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(FileIdForVersionTextBox.Text.Trim(), out int fileId))
            {
                MessageBox.Show("Please enter a valid File ID.", "Invalid File ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                var versions = await _connection.InvokeAsync<List<DocumentVersion>>("GetDocumentVersionsById", fileId);
                FileVersionsListBox.Items.Clear();
                if (versions != null && versions.Any())
                {
                    foreach (var doc in versions)
                    {
                        FileVersionsListBox.Items.Add($"FileID: {doc.Id}, Version: {doc.Version}, Uploaded: {doc.UploadTimestamp}");
                    }
                }
                else
                {
                    FileVersionsListBox.Items.Add("No versions found.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading versions: " + ex.Message);
            }
        }

        private async void LoadAllFilesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var allFiles = await _connection.InvokeAsync<List<DocumentVersion>>("GetAllDocuments");
                AllFilesListBox.Items.Clear();
                if (allFiles != null && allFiles.Any())
                {
                    foreach (var doc in allFiles)
                    {
                        AllFilesListBox.Items.Add($"FileID: {doc.Id}, Name: {doc.Filename}, Version: {doc.Version}, Author: {doc.Author}, Uploaded: {doc.UploadTimestamp}");
                    }
                }
                else
                {
                    AllFilesListBox.Items.Add("No files found.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading all files: " + ex.Message);
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
            if (string.IsNullOrEmpty(groupName))
                return;
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
            if (string.IsNullOrEmpty(groupName))
                return;
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
            if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(message))
                return;
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
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OpenPrivateWhiteboardButton_Click(object sender, RoutedEventArgs e)
        {
            var targetUser = PrivateTargetTextBox.Text.Trim();
            if (string.IsNullOrEmpty(targetUser))
            {
                MessageBox.Show("Please enter a target username.", "Missing Target", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var plugin = _currentlyLoadedPlugins.FirstOrDefault(p => p.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase));
            if (plugin == null)
            {
                MessageBox.Show("Whiteboard plugin is NOT loaded.\nPlease go to the 'Plugins' tab, load the plugin manually, and then try again.", "Plugin not found", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("Please enter a group name.", "Missing Group Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var plugin = _currentlyLoadedPlugins.FirstOrDefault(p => p.Name.Equals("Whiteboard", StringComparison.OrdinalIgnoreCase));
            if (plugin == null)
            {
                MessageBox.Show("Whiteboard plugin is NOT loaded.\nPlease go to the 'Plugins' tab, load the plugin manually, and then try again.", "Plugin not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var initMethod = plugin.GetType().GetMethod("Initialize", new[] { typeof(HubConnection), typeof(string), typeof(bool) });
            if (initMethod != null)
            {
                initMethod.Invoke(plugin, new object[] { _connection, groupName, true });
            }
            plugin.Execute();
        }
    }
}
