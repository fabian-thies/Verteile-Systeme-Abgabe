using System;
using System.Windows;

namespace Client
{
    public partial class MainWindow : Window
    {
        private ChatClient _chatClient = new ChatClient();

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            bool connected = _chatClient.Connect("127.0.0.1", 5000);
            if (connected)
            {
                ChatMessages.Items.Add("Connected to server.");

                // Start receiving messages in background
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
                _chatClient.SendMessage(msg);
                MessageInput.Text = string.Empty;
            }
        }

        // This method will be called whenever a message arrives from the server
        private void ReceiveMessage(string message)
        {
            // We need to update the UI thread, so use Dispatcher
            Dispatcher.Invoke(() =>
            {
                ChatMessages.Items.Add("Server: " + message);
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _chatClient.Disconnect();
        }
    }
}