// MainWindow.xaml.cs
using System;
using System.Threading.Tasks;
using System.Windows;

namespace Client
{
    public partial class MainWindow : Window
    {
        private ChatClient _chatClient;
        private string _username;

        // Constructor receives an already connected ChatClient and the username
        public MainWindow(ChatClient chatClient, string username)
        {
            InitializeComponent();
            _chatClient = chatClient;
            _username = username;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            // The connection should already be established by the login window.
            // Falls doch eine erneute Verbindung benötigt wird:
            if (_chatClient != null && _chatClient.Connect("127.0.0.1", 5000))
            {
                ChatMessages.Items.Add("Connected to server.");
                // Start receiving messages in the background
                await Task.Run(() => _chatClient.ReceiveMessages(ReceiveMessage));
            }
            else
            {
                ChatMessages.Items.Add("Failed to connect.");
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string msg = MessageInput.Text;
            if (!string.IsNullOrWhiteSpace(msg))
            {
                // Send chat message without any command prefix (server already knows the username)
                _chatClient.SendMessage(msg);
                MessageInput.Text = string.Empty;
            }
        }

        // This method will be called whenever a message arrives from the server
        private void ReceiveMessage(string message)
        {
            // Update the UI on the main thread
            Dispatcher.Invoke(() =>
            {
                ChatMessages.Items.Add(message);
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _chatClient.Disconnect();
        }
    }
}
