// MainWindow.xaml.cs
// English comments:
// The MainWindow receives the ChatClient instance from the LoginWindow.
// Do not start a new ReceiveMessages loop here – use the one already running.
using System;
using System.Windows;

namespace Client
{
    public partial class MainWindow : Window
    {
        private ChatClient _chatClient;
        private string _username;

        public MainWindow(ChatClient chatClient, string username)
        {
            InitializeComponent();
            _chatClient = chatClient;
            _username = username;
            // Do NOT start a new ReceiveMessages loop here.
            // The one from LoginWindow continues to run.
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

        // Use this method to update the chat window.
        // It will be called by the single running ReceiveMessages loop.
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