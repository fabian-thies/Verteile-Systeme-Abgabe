// LoginWindow.xaml.cs
// English comments:
// In this revised version, the connection is established only once in the constructor.
// The ReceiveMessages loop is started here with OnServerMessage as callback.
// After login, we pass the same ChatClient instance to MainWindow without starting a new Receive loop.
using System;
using System.Threading.Tasks;
using System.Windows;

namespace Client
{
    public partial class LoginWindow : Window
    {
        private ChatClient _chatClient = new ChatClient();
        private string _lastServerMessage = "";

        public LoginWindow()
        {
            InitializeComponent();

            // Establish connection only once.
            if (!_chatClient.Connect("127.0.0.1", 5000))
            {
                StatusText.Text = "Failed to connect to server.";
            }
            else
            {
                // Start receiving messages from the server (only one loop).
                Task.Run(() => _chatClient.ReceiveMessages(OnServerMessage));
            }
        }

        private void OnServerMessage(string message)
        {
            // Update the UI thread.
            Dispatcher.Invoke(() =>
            {
                _lastServerMessage = message;
                // Optionally, for debugging:
                // DebugText.Text = message;
            });
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameInput.Text;
            string password = PasswordInput.Password;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                StatusText.Text = "Please enter both username and password.";
                return;
            }

            string loginCommand = $"/login {username} {password}";
            _chatClient.SendMessage(loginCommand);

            // Wait for server response.
            await Task.Delay(1000);

            if (_lastServerMessage.Contains("Login successful"))
            {
                // Open the chat window, using the same ChatClient instance.
                MainWindow chatWindow = new MainWindow(_chatClient, username);
                chatWindow.Show();
                this.Close();
            }
            else
            {
                StatusText.Text = "Login failed. Check your credentials.";
            }
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameInput.Text;
            string password = PasswordInput.Password;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                StatusText.Text = "Please enter both username and password.";
                return;
            }

            string registerCommand = $"/register {username} {password}";
            _chatClient.SendMessage(registerCommand);

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
