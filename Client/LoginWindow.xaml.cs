// English comments:
// Revised LoginWindow.xaml.cs
// - The connection is established in the constructor.
// - Received messages are captured via a callback (OnServerMessage) and stored.
// - On login/register button click, the code waits and checks if the response indicates success.
using System;
using System.Threading.Tasks;
using System.Windows;

namespace Client
{
    public partial class LoginWindow : Window
    {
        // Shared ChatClient instance used for connecting and authenticating.
        private ChatClient _chatClient = new ChatClient();
        
        // Variable to store the latest server message.
        private string _lastServerMessage = "";

        public LoginWindow()
        {
            InitializeComponent();

            // Establish connection only once in the constructor.
            if (!_chatClient.Connect("127.0.0.1", 5000))
            {
                StatusText.Text = "Failed to connect to server.";
            }
            else
            {
                // Start receiving messages from the server.
                Task.Run(() => _chatClient.ReceiveMessages(OnServerMessage));
            }
        }

        // Callback to handle incoming server messages.
        private void OnServerMessage(string message)
        {
            // Ensure we update the UI thread.
            Dispatcher.Invoke(() =>
            {
                // Update _lastServerMessage with the received message.
                _lastServerMessage = message;
                // Optional: Update a UI element to show server messages for debugging.
                // For example: DebugText.Text = message;
            });
        }

        // Event handler for the Login button click.
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameInput.Text;
            string password = PasswordInput.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                StatusText.Text = "Please enter both username and password.";
                return;
            }

            // Send login command to the server.
            string loginCommand = $"/login {username} {password}";
            _chatClient.SendMessage(loginCommand);

            // Wait for the server response.
            await Task.Delay(1000);

            // Check if the server response indicates a successful login.
            if (_lastServerMessage.Contains("Login successful"))
            {
                // Open the chat window.
                MainWindow chatWindow = new MainWindow(_chatClient, username);
                chatWindow.Show();
                this.Close();
            }
            else
            {
                // Remain in the LoginWindow and show the error message.
                StatusText.Text = "Login failed. Check your credentials.";
            }
        }

        // Event handler for the Register button click.
        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameInput.Text;
            string password = PasswordInput.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                StatusText.Text = "Please enter both username and password.";
                return;
            }

            // Send register command to the server.
            string registerCommand = $"/register {username} {password}";
            _chatClient.SendMessage(registerCommand);

            // Wait for the server response.
            await Task.Delay(1000);

            if (_lastServerMessage.Contains("Registration successful"))
            {
                StatusText.Text = "Registration successful! Please click Login.";
            }
            else
            {
                StatusText.Text = "Registration failed. Username might already exist.";
            }
        }
    }
}
