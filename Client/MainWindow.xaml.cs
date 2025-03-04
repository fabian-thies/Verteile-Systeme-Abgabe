// MainWindow.xaml.cs
using System;
using System.Threading.Tasks;
using System.Windows;

namespace Client
{
    public partial class MainWindow : Window
    {
        private ChatClient _chatClient = new ChatClient();
        private bool _isLoggedIn = false;

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

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Send login command to the server
            string username = UsernameInput.Text;
            string password = PasswordInput.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ChatMessages.Items.Add("Please enter both username and password.");
                return;
            }

            // Construct login command (the protocol defined on the server)
            string loginCommand = $"/login {username} {password}";
            _chatClient.SendMessage(loginCommand);

            // Wait a moment for server response
            await Task.Delay(500);
            // In a real application, you would parse the server response asynchronously
            // and then update _isLoggedIn accordingly. For simplicity, assume login is successful if server replies "Login successful!".
            // Here wir rely on the user to see the chat message.
            // Enable message input if logged in (this could be improved by proper response parsing).
            _isLoggedIn = true; // Set this based on actual server response in production
            MessageInput.IsEnabled = true;
            SendButton.IsEnabled = true;
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
