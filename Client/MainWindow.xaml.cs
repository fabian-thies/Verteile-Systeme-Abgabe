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

        // We get the same connected ChatClient from the login window
        public MainWindow(ChatClient chatClient, string username)
        {
            InitializeComponent();
            _chatClient = chatClient;
            _username = username;

            // Optionally, start receiving messages right away if not started yet
            Task.Run(() => _chatClient.ReceiveMessages(ReceiveMessage));
        }

        // Remove or disable this connect button if it re-connects:
        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            // If you REALLY want to reconnect, you can do so, 
            // but you'd have to re-login on the server side too.
            // Usually you'd do:
            ChatMessages.Items.Add("Already connected (from LoginWindow).");
            // or remove the button entirely.
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

        private void ReceiveMessage(string message)
        {
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